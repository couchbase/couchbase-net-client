using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Http;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Providers.Streaming
{
    /// <summary>
    /// A comet style streaming HTTP connection provider for Couchbase configurations.
    /// </summary>
    internal sealed class HttpStreamingProvider : ConfigProviderBase
    {
        private IServerConfig _serverConfig;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly ConcurrentDictionary<string, Thread> _threads = new ConcurrentDictionary<string, Thread>();
        private static readonly CountdownEvent CountdownEvent = new CountdownEvent(1);
        private static readonly AutoResetEvent RegisterEvent = new AutoResetEvent(true);
        private volatile bool _disposed;

        public HttpStreamingProvider(ClientConfiguration clientConfig,
            Func<IConnectionPool, IIOService> ioServiceFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IIOService, ITypeTranscoder, ISaslMechanism> saslFactory,
            IByteConverter converter,
            ITypeTranscoder transcoder)
            : base(clientConfig, ioServiceFactory, connectionPoolFactory, saslFactory, converter, transcoder)
        {
        }

        /// <summary>
        /// Starts the HTTP streaming connection to the Couchbase Server and gets the latest configuration for a SASL authenticated Bucket.
        /// </summary>
        /// <param name="bucketName">The name of the Couchbase Bucket.</param>
        /// <param name="password">The SASL password used to connect to the Bucket.</param>
        /// <returns>A <see cref="IConfigInfo"/> object representing the latest configuration.</returns>
        public override IConfigInfo GetConfig(string bucketName, string username, string password)
        {
            var bucketConfiguration = GetOrCreateConfiguration(bucketName);
            StartProvider(bucketName, password);
            var bucketConfig = GetBucketConfig(bucketName, password);

            try
            {
                ConfigLock.EnterWriteLock();
                IConfigInfo configInfo = null;
                var nodes = bucketConfig.Nodes.ToList();
                while (nodes.Any())
                {
                    try
                    {
                        nodes.Shuffle();
                        var node = nodes.First();
                        nodes.Remove(node);

                        IBucketConfig newConfig;
                        var uri = bucketConfig.GetTerseUri(node, bucketConfiguration.UseSsl);

                        using (new SynchronizationContextExclusion())
                        {
                            using (var httpClient = new CouchbaseHttpClient(username, password))
                            {
                                var body = httpClient.GetStringAsync(uri).Result;
                                body = body.Replace("$HOST", uri.Host);
                                newConfig = JsonConvert.DeserializeObject<BucketConfig>(body);
                            }
                        }

                        newConfig.Password = password;
                        configInfo = CreateConfigInfo(newConfig);
                        Configs[bucketName] = configInfo;
                        break;

                    }
                    catch (AggregateException e)
                    {
                        Log.Error(e.InnerException);
                    }
                    catch (IOException e)
                    {
                        Log.Error(e);
                    }
                }

                if (configInfo == null)
                {
                    throw new BucketNotFoundException();
                }
                return configInfo;
            }
            finally
            {
                ConfigLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Registers an <see cref="IConfigObserver"/> object, which is notified when a configuration changes.
        /// </summary>
        /// <param name="observer">The <see cref="IConfigObserver"/> that will be notified when a configuration
        /// update occurs. These are Memcached and Couchbase Buckets.</param>
        /// <returns>True if the observer was registered without failure.</returns>
        public override bool RegisterObserver(IConfigObserver observer)
        {
            RegisterEvent.WaitOne(10000);//TODO make configurable
            var hasRegistered = false;

            try
            {
                var bucketConfig = _serverConfig.Buckets.Find(x => x.Name == observer.Name);
                if (bucketConfig == null)
                {
                    throw new BucketNotFoundException(observer.Name);
                }

                var cancellationTokenSource = new CancellationTokenSource();
                _cancellationTokens[observer.Name] = cancellationTokenSource;

                var configThreadState = new ConfigThreadState(bucketConfig, ConfigChangedHandler, ErrorOccurredHandler,
                    cancellationTokenSource.Token);
                var thread = new Thread(configThreadState.ListenForConfigChanges)
                {
                    IsBackground = true,
                    Name = "stmconfig"
                };

                if (_threads.TryAdd(observer.Name, thread) && ConfigObservers.TryAdd(observer.Name, observer))
                {
                    _threads[observer.Name].Start();
                    if (CountdownEvent.CurrentCount == 0)
                    {
                        CountdownEvent.Reset(1);
                    }

                    //TODO add timeout?
                    CountdownEvent.Wait(10000, cancellationTokenSource.Token);//TODO make configurable
                    hasRegistered = true;
                }
            }
            finally
            {
                RegisterEvent.Set();
            }
            return hasRegistered;
        }

        /// <summary>
        /// Raised when a configuration update has occurred. All observers will be notified of the changes.
        /// </summary>
        /// <param name="bucketConfig">The new configuration.</param>
        void ConfigChangedHandler(IBucketConfig bucketConfig)
        {
            try
            {
                ConfigLock.EnterWriteLock();
                IConfigObserver configObserver;
                if (!ConfigObservers.TryGetValue(bucketConfig.Name, out configObserver))
                {
                    // Observer has been unregistered
                    return;
                }

                IConfigInfo configInfo;
                if (Configs.TryGetValue(configObserver.Name, out configInfo))
                {
                    var staleBucketConfig = configInfo.BucketConfig;

                    Log.Info("Config changed new Rev#{0} | old Rev#{1} HTTP: {2}",
                        bucketConfig.Rev, staleBucketConfig.Rev, JsonConvert.SerializeObject(bucketConfig));

                    if (bucketConfig.Rev > staleBucketConfig.Rev)
                    {
                        configInfo.LoadConfig(bucketConfig);
                    }
                }
                else
                {
                    configInfo = CreateConfigInfo(bucketConfig);
                    Configs.TryAdd(bucketConfig.Name, configInfo);
                }

                try
                {
                    ClientConfig.UpdateBootstrapList(bucketConfig);
                    configObserver.NotifyConfigChanged(configInfo);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
                SignalCountdownEvent();
            }
            finally
            {
                ConfigLock.ExitWriteLock();
            }
        }

        void SignalCountdownEvent()
        {
            if (CountdownEvent.CurrentCount > 0)
            {
                CountdownEvent.Signal();
            }
        }

        /// <summary>
        /// Creates a Bucket specific <see cref="IConfigInfo"/> instance.
        /// </summary>
        /// <param name="bucketConfig">The <see cref="IBucketConfig"/> to use for client configuration.</param>
        /// <returns></returns>
        IConfigInfo CreateConfigInfo(IBucketConfig bucketConfig)
        {
            IConfigInfo configInfo;
            switch (bucketConfig.NodeLocator.ToEnum<NodeLocatorEnum>())
            {
                case NodeLocatorEnum.VBucket:
                    configInfo = new CouchbaseConfigContext(bucketConfig,
                        ClientConfig,
                        IOServiceFactory,
                        ConnectionPoolFactory,
                        SaslFactory,
                        Transcoder);
                    break;
                case NodeLocatorEnum.Ketama:
                    configInfo = new MemcachedConfigContext(bucketConfig,
                        ClientConfig,
                        IOServiceFactory,
                        ConnectionPoolFactory,
                        SaslFactory,
                        Transcoder);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            configInfo.LoadConfig();
            return configInfo;
        }

        /// <summary>
        /// Starts the HTTP streaming connection.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        void StartProvider(string username, string password)
        {
            _serverConfig = new HttpServerConfig(ClientConfig, username, password);
            _serverConfig.Initialize();
            Log.Debug("Starting provider on main thread: {0}", Thread.CurrentThread.ManagedThreadId);
        }

        public IServerConfig GetCachedServerConfig()
        {
            return _serverConfig;
        }

        IBucketConfig GetBucketConfig(string bucketName, string password)
        {
            try
            {
                ConfigLock.EnterReadLock();
                var bucketConfig = _serverConfig.Buckets.Find(x => x.Name == bucketName);
                if (bucketConfig == null)
                {
                    throw new BucketNotFoundException(bucketName);
                }
                bucketConfig.Password = password;
                return bucketConfig;
            }
            finally
            {
                ConfigLock.ExitReadLock();
            }
        }

        void ErrorOccurredHandler(IBucketConfig bucketConfig)
        {
            //TODO provide implementation to begin the bootstrapping procss from the beginning
        }

        /// <summary>
        /// Un-registers an observer, which is either a Couchbase or Memcached Bucket, from the provider.
        /// </summary>
        /// <param name="observer"></param>
        public override void UnRegisterObserver(IConfigObserver observer)
        {
            try
            {
                ConfigLock.EnterWriteLock();
                Thread thread;
                if (_threads.TryRemove(observer.Name, out thread))
                {
                    CancellationTokenSource cancellationTokenSource;
                    if (_cancellationTokens.TryRemove(observer.Name, out cancellationTokenSource))
                    {
                        Log.Info("Cancelling {0}", observer.Name);
                        cancellationTokenSource.Cancel();
                        cancellationTokenSource.Dispose();
                    }

                    IConfigObserver temp;
                    if (ConfigObservers.TryRemove(observer.Name, out temp))
                    {
                        Log.Info("Removing observer for {0}", observer.Name);
                    }

                    IConfigInfo configInfo;
                    if (Configs.TryRemove(observer.Name, out configInfo))
                    {
                        configInfo.Dispose();
                        Log.Info("Removing config for {0}", observer.Name);
                    }
                }
            }
            finally
            {
                ConfigLock.ExitWriteLock();
            }
        }

        public override void Dispose()
        {
            Dispose(true);
        }

        public void Dispose(bool disposing)
        {
            try
            {
                ConfigLock.EnterWriteLock();
                {
                    if (!_disposed && disposing)
                    {
                        GC.SuppressFinalize(this);
                    }
                    foreach (var configObserver in ConfigObservers)
                    {
                        UnRegisterObserver(configObserver.Value);
                    }
                    ConfigObservers.Clear();
                    _threads.Clear();
                    _disposed = true;
                }
            }
            finally
            {
                ConfigLock.ExitWriteLock();
            }
        }

#if DEBUG
        ~HttpStreamingProvider()
        {
            Dispose(true);
        }
#endif
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion

