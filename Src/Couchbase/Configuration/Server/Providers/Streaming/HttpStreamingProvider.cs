using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Providers.Streaming
{
    /// <summary>
    /// A comet style streaming HTTP connection provider for Couchbase configurations.
    /// </summary>
    internal sealed class HttpStreamingProvider : IConfigProvider
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private IServerConfig _serverConfig;
        private readonly ClientConfiguration _clientConfig;

        private readonly Func<IConnectionPool, IOStrategy> _ioStrategyFactory;
        private readonly Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>(); 
        private readonly ConcurrentDictionary<string, Thread> _threads = new ConcurrentDictionary<string, Thread>(); 
        private readonly ConcurrentDictionary<string, IConfigInfo> _configs = new ConcurrentDictionary<string, IConfigInfo>();
        private readonly ConcurrentDictionary<string, IConfigObserver> _observers = new ConcurrentDictionary<string, IConfigObserver>();
        private static readonly CountdownEvent CountdownEvent = new CountdownEvent(1);
        private volatile bool _disposed;

        public HttpStreamingProvider(ClientConfiguration clientConfig)
        {
            _clientConfig = clientConfig;
        }

        public HttpStreamingProvider(ClientConfiguration clientConfig,
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory)
        {
            _clientConfig = clientConfig;
            _ioStrategyFactory = ioStrategyFactory;
            _connectionPoolFactory = connectionPoolFactory;
        }

        /// <summary>
        /// Gets the currently cached (and used) configuration.
        /// </summary>
        /// <param name="bucketName">The name of the Couchbase Bucket used to lookup the <see cref="IConfigInfo"/> object.</param>
        /// <returns></returns>
        public IConfigInfo GetCached(string bucketName)
        {
            IConfigInfo configInfo;
            if (!_configs.TryGetValue(bucketName, out configInfo))
            {
                throw new ConfigNotFoundException(bucketName);
            }
            return configInfo;
        }

        /// <summary>
        /// Starts the HTTP streaming connection to the Couchbase Server and gets the latest configuration for a SASL authenticated Bucket.
        /// </summary>
        /// <param name="bucketName">The name of the Couchbase Bucket.</param>
        /// <param name="password">The SASL password used to connect to the Bucket.</param>
        /// <returns>A <see cref="IConfigInfo"/> object representing the latest configuration.</returns>
        public IConfigInfo GetConfig(string bucketName, string password)
        {
            StartProvider(bucketName, password);
            var bucketConfig = GetBucketConfig(bucketName, password);

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
                    var uri = bucketConfig.GetTerseUri(node);
                    using (var webClient = new AuthenticatingWebClient(bucketName, password))
                    {
                        var body = webClient.DownloadString(uri);
                        newConfig = JsonConvert.DeserializeObject<BucketConfig>(body);
                    }

                    configInfo = CreateConfigInfo(newConfig);
                    _configs[bucketName] = configInfo;
                    break;

                }
                catch (WebException e)
                {
                    Log.Error(e);
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

        /// <summary>
        /// Starts the HTTP streaming connection to the Couchbase Server and gets the latest configuration for a non-SASL authenticated Bucket.
        /// </summary>
        /// <param name="bucketName">The name of the Couchbase Bucket.</param>
        /// <returns>A <see cref="IConfigInfo"/> object representing the latest configuration.</returns>
        public IConfigInfo GetConfig(string bucketName)
        {
            return GetConfig(bucketName, string.Empty);
        }

        /// <summary>
        /// Registers an <see cref="IConfigObserver"/> object, which is notified when a configuration changes.
        /// </summary>
        /// <param name="observer">The <see cref="IConfigObserver"/> that will be notified when a configuration 
        /// update occurs. These are Memcached and Couchbase Buckets.</param>
        /// <returns>True if the observer was registered without failure.</returns>
        public bool RegisterObserver(IConfigObserver observer)
        {
            var bucketConfig = _serverConfig.Buckets.Find(x => x.Name == observer.Name);
            if (bucketConfig == null)
            {
                throw new BucketNotFoundException(observer.Name);
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokens[observer.Name] = cancellationTokenSource;

            var configThreadState = new ConfigThreadState(bucketConfig, ConfigChangedHandler, ErrorOccurredHandler, cancellationTokenSource.Token);
            var thread = new Thread(configThreadState.ListenForConfigChanges);
            
            if (_threads.TryAdd(observer.Name, thread) && _observers.TryAdd(observer.Name, observer))
            {
                _threads[observer.Name].Start();
                
                if (CountdownEvent.CurrentCount == 0)
                {
                    CountdownEvent.Reset(1);
                }

                CountdownEvent.Wait();
            }
            return true;//todo fix
        }

        /// <summary>
        /// Raised when a configuration update has occurred. All observers will be notified of the changes.
        /// </summary>
        /// <param name="bucketConfig"></param>
        private void ConfigChangedHandler(IBucketConfig bucketConfig)
        {
            var configObserver = _observers[bucketConfig.Name];

            IConfigInfo configInfo;
            if (_configs.ContainsKey(bucketConfig.Name))
            {
                configInfo = _configs[bucketConfig.Name];
                if (configInfo.BucketConfig.Equals(bucketConfig))
                {
                    SignalCountdownEvent();
                    return;
                }
                configInfo = CreateConfigInfo(bucketConfig);
            }
            else
            {
                configInfo = CreateConfigInfo(bucketConfig);
                _configs.TryAdd(bucketConfig.Name, configInfo);
            }
            try
            {
                configObserver.NotifyConfigChanged(configInfo);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            SignalCountdownEvent();
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
            IConfigInfo configInfo = null;
            switch (bucketConfig.NodeLocator.ToEnum<NodeLocatorEnum>())
            {
                case NodeLocatorEnum.VBucket:
                    configInfo = new CouchbaseConfigContext(bucketConfig,
                        _clientConfig,
                        _ioStrategyFactory,
                        _connectionPoolFactory);
                    break;
                case NodeLocatorEnum.Ketama:
                    configInfo = new MemcachedConfigContext(bucketConfig,
                        _clientConfig,
                        _ioStrategyFactory,
                        _connectionPoolFactory);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return configInfo;
        }

        /// <summary>
        /// Starts the HTTP streaming connection.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        void StartProvider(string username, string password)
        {
            _serverConfig = new HttpServerConfig(_clientConfig, username, password);
            _serverConfig.Initialize();
            Log.Debug(m => m("Starting provider on main thread: {0}", Thread.CurrentThread.ManagedThreadId));
        }

        IBucketConfig GetBucketConfig(string bucketName, string password)
        {
            var bucketConfig = _serverConfig.Buckets.Find(x => x.Name == bucketName);
            if (bucketConfig == null)
            {
                throw new BucketNotFoundException(bucketName);
            }
            bucketConfig.Password = password;
            return bucketConfig;
        }

        void ErrorOccurredHandler(IBucketConfig bucketConfig)
        {
            //TODO provide implementation to begin the bootstrapping procss from the beginning
        }

        /// <summary>
        /// Un-registers an observer, which is either a Couchbase or Memcached Bucket, from the Provider.
        /// </summary>
        /// <param name="observer"></param>
        public void UnRegisterObserver(IConfigObserver observer)
        {
            Thread thread;
            if (_threads.TryRemove(observer.Name, out thread))
            {
                CancellationTokenSource cancellationTokenSource;
                if (_cancellationTokens.TryRemove(observer.Name, out cancellationTokenSource))
                {
                    Log.Info(m=>m("Cancelling {0}", observer.Name));
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                }

                IConfigObserver temp;
                if (_observers.TryRemove(observer.Name, out temp))
                {
                    Log.Info(m=>m("Removing observer for {0}", observer.Name));
                }

                IConfigInfo configInfo = null;
                if (_configs.TryRemove(observer.Name, out configInfo))
                {
                    Log.Info(m=>m("Removing config for {0}", observer.Name));
                }
            }
        }

        /// <summary>
        /// Checks to see if an observer has been registered.
        /// </summary>
        /// <param name="observer"></param>
        /// <returns></returns>
        public bool ObserverExists(IConfigObserver observer)
        {
            return _observers.ContainsKey(observer.Name);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                GC.SuppressFinalize(this);
            }
            foreach (var configObserver in _observers)
            {
                UnRegisterObserver(configObserver.Value);
            }
            _observers.Clear();
            _threads.Clear();
            _disposed = true;
        }

        ~HttpStreamingProvider()
        {
            Dispose(true);
        }
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

