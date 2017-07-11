using Couchbase.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Buckets;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Couchbase.Authentication;
using Couchbase.Core.Monitoring;
using Couchbase.Configuration.Server.Monitoring;
using Couchbase.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.Core
{
    internal sealed class ClusterController : IClusterController
    {
        private static readonly ILog Log = LogManager.GetLogger<ClusterController>();
        private readonly ClientConfiguration _clientConfig;
        private readonly ConcurrentDictionary<string, IBucket> _buckets = new ConcurrentDictionary<string, IBucket>();
        private readonly List<IConfigProvider> _configProviders = new List<IConfigProvider>();
        private readonly Func<IConnectionPool, IIOService> _ioServiceFactory;
        private readonly Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private readonly Func<string, string, IConnectionPool, ITypeTranscoder, ISaslMechanism> _saslFactory;
        private readonly ClusterMonitor _clusterMonitor;
        private readonly object _syncObject = new object();
        private volatile bool _disposed;
        private readonly ConfigMonitor _configMonitor;
        private readonly BlockingCollection<IBucketConfig> _configQueue = new BlockingCollection<IBucketConfig>(new ConcurrentQueue<IBucketConfig>());
        private readonly Thread _configThread;

        public ClusterController(ClientConfiguration clientConfig)
            : this(clientConfig,
            clientConfig.IOServiceCreator,
            clientConfig.ConnectionPoolCreator,
            clientConfig.CreateSaslMechanism,
            clientConfig.Converter(),
            clientConfig.Transcoder())
        {
        }

        public ClusterController(ICluster cluster, ClientConfiguration clientConfig)
            : this(clientConfig)
        {
            Cluster = cluster;
        }

        public ClusterController(ClientConfiguration clientConfig,
            Func<IConnectionPool, IIOService> ioServiceFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IConnectionPool, ITypeTranscoder, ISaslMechanism> saslFactory,
            IByteConverter converter,
            ITypeTranscoder transcoder)
        {
            _clientConfig = clientConfig;
            _ioServiceFactory = ioServiceFactory;
            _connectionPoolFactory = connectionPoolFactory;
            _saslFactory = saslFactory;
            Converter = converter;
            Transcoder = transcoder;
            ServerConfigTranscoder = new DefaultTranscoder();
            Initialize();

            if (clientConfig.EnableDeadServiceUriPing)
            {
                _clusterMonitor = new ClusterMonitor(this);
                _clusterMonitor.StartMonitoring();
            }

            LastConfigCheckedTime = DateTime.Now;
            if (Configuration.EnableConfigHeartBeat)
            {
                _configMonitor = new ConfigMonitor(this);
                _configMonitor.StartMonitoring();
            }

            _configThread = new Thread(ProcessConfig)
            {
                Name = "CT",
                IsBackground = true
            };
            _configThread.Start();
        }

        /// <summary>
        /// "Consumer" for config processing on a single thread.
        /// </summary>
        internal void ProcessConfig()
        {
            foreach (var config in _configQueue.GetConsumingEnumerable())
            {
                foreach (var provider in _configProviders.OfType<CarrierPublicationProvider>())
                {
                    Log.Debug("Processing config rev#{0}", config.Rev);
                    provider.UpdateConfig(config);
                }
            }
        }

        /// <summary>
        /// Enqueues the configuration for processing by the configuration thread "CT"; Any thread can "Produce" a configuration for processing.
        /// </summary>
        /// <param name="config">The cluster map to check.</param>
        public void EnqueueConfigForProcessing(IBucketConfig config)
        {
            Log.Debug("Queueing config rev#{0} for [{1}].", config.Rev, config.Name);
            _configQueue.Add(config);
        }

        public DateTime LastConfigCheckedTime { get; set; }

        public ICluster Cluster { get; private set; }

        public IByteConverter Converter { get; private set; }

        public ITypeTranscoder Transcoder { get; private set; }

        public ITypeTranscoder ServerConfigTranscoder { get; private set; }

        public List<IConfigProvider> ConfigProviders { get { return _configProviders; } }

        private void Initialize()
        {
            _clientConfig.Initialize();
            _configProviders.Add(new CarrierPublicationProvider(_clientConfig,
                _ioServiceFactory,
                _connectionPoolFactory,
                _saslFactory,
                Converter,
                Transcoder));

            _configProviders.Add(new HttpStreamingProvider(_clientConfig,
                _ioServiceFactory,
                _connectionPoolFactory,
                _saslFactory,
                Converter,
                Transcoder));
        }

        public IConfigProvider GetProvider(string name)
        {
            throw new NotImplementedException();
        }

        public void NotifyConfigPublished(IBucketConfig bucketConfig, bool force = false)
        {
            EnqueueConfigForProcessing(bucketConfig);
        }

        public IBucket CreateBucket(string bucketName, IAuthenticator authenticator = null)
        {
            var username = bucketName;
            var password = string.Empty;

            if (authenticator == null)
            {
                //try to find a password in configuration
                BucketConfiguration bucketConfig;
                if (_clientConfig.BucketConfigs.TryGetValue(bucketName, out bucketConfig)
                    && bucketConfig.Password != null)
                {
                    bucketName = bucketConfig.BucketName;
                    password = bucketConfig.Password;
                }
            }
            else
            {
                var bucketCredentials = authenticator.GetCredentials(AuthContext.BucketKv, bucketName);
                switch (authenticator.AuthenticatorType)
                {
                    case AuthenticatorType.Classic:
                        if (bucketCredentials.ContainsKey(bucketName))
                        {
                            username = bucketName;
                            password = bucketCredentials.First().Value;
                        }
                        else
                        {
                            throw new BucketNotFoundException(string.Format("Could not find credentials for bucket: {0}", bucketName));
                        }
                        break;
                    case AuthenticatorType.Password:
                        username = bucketCredentials.First().Key;
                        password = bucketCredentials.First().Value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return CreateBucket(bucketName, username, password, authenticator);
        }

        public IBucket CreateBucket(string bucketName, string username, string password, IAuthenticator authenticator = null)
        {
            var exceptions = new List<Exception>();
            lock (_syncObject)
            {
                //shortcircuit in case lock was waited upon because another thread bootstraped same bucket
                if (_buckets.ContainsKey(bucketName))
                {
                    IBucket existingBucket = _buckets[bucketName];
                    if ((existingBucket as IRefCountable).AddRef() != -1)
                    {
                        Log.Debug("Bootstraping was already done, returning existing bucket {0}", bucketName);
                        return existingBucket; // This is the only short circuit. All other cases fall through to bootstrapping.
                    }
                    else
                    {
                        Log.Debug("Bucket dictionary contained disposed bucket. Bootstrapping {0}.", bucketName);
                        DestroyBucket(existingBucket);
                    }
                }
                //otherwise bootstrap a new bucket
                var success = false;
                IBucket bucket = null;
                foreach (var provider in _configProviders)
                {
                    try
                    {
                        Log.Debug("Trying to bootstrap with {0}.", provider);
                        var config = provider.GetConfig(bucketName, username, password);
                        IRefCountable refCountable = null;
                        switch (config.NodeLocator)
                        {
                            case NodeLocatorEnum.VBucket:
                                bucket = _buckets.GetOrAdd(bucketName,
                                    name => new CouchbaseBucket(this, bucketName, Converter, Transcoder, authenticator));
                                refCountable = bucket as IRefCountable;
                                if (refCountable != null)
                                {
                                    refCountable.AddRef();
                                }
                                break;

                            case NodeLocatorEnum.Ketama:
                                bucket = _buckets.GetOrAdd(bucketName,
                                    name => new MemcachedBucket(this, bucketName, Converter, Transcoder, authenticator));
                                refCountable = bucket as IRefCountable;
                                if (refCountable != null)
                                {
                                    refCountable.AddRef();
                                }
                                break;

                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        var configObserver = (IConfigObserver) bucket;
                        if (provider.ObserverExists(configObserver))
                        {
                            Log.Debug("Using existing bootstrap {0}.", provider);
                            _clientConfig.UpdateBootstrapList(config.BucketConfig);

                            configObserver.NotifyConfigChanged(config);
                            success = true;
                            break;
                        }

                        if (provider.RegisterObserver(configObserver) &&
                            _buckets.TryAdd(bucket.Name, bucket))
                        {
                            Log.Debug("Successfully bootstrapped using {0}.", provider);
                            _clientConfig.UpdateBootstrapList(config.BucketConfig);
                            configObserver.NotifyConfigChanged(config);
                            success = true;
                            break;
                        }
                        _clientConfig.UpdateBootstrapList(config.BucketConfig);
                        configObserver.NotifyConfigChanged(config);
                        success = true;
                        break;
                    }
                    catch (Exception e)
                    {
                        Log.Warn(e);
                        exceptions.Add(e);
                    }
                }

                if (!success)
                {
                    throw new AggregateException("Could not bootstrap - check inner exceptions for details.", exceptions);
                }
                return bucket;
            }
        }

        public void DestroyBucket(IBucket bucket)
        {
            IBucket temp;
            if (_buckets.TryRemove(bucket.Name, out temp))
            {
                var configObserver = temp as IConfigObserver;
                foreach (var configProvider in ConfigProviders)
                {
                    configProvider.UnRegisterObserver(configObserver);
                }
            }
        }

        /// <summary>
        /// Gets the first <see cref="CouchbaseBucket"/> instance found./>
        /// </summary>
        /// <returns></returns>
        public IBucket GetBucket(IAuthenticator authenticator)
        {
            if (_buckets.IsEmpty)
            {
                if (authenticator.AuthenticatorType != AuthenticatorType.Classic)
                {
                    throw new NotSupportedException("Only ClassicAuthenticator supports storing bucket names.");
                }

                lock (_syncObject)
                {
                    if (_buckets.IsEmpty)
                    {
                        var classicAuthenticator = (ClassicAuthenticator) authenticator;
                        var bucketName = classicAuthenticator.BucketCredentials.First().Key;
                        return CreateBucket(bucketName, authenticator);
                    }
                }
            }
            return _buckets.First().Value;
        }

        /// <summary>
        /// Retrieve Information for this cluster, see <see cref="ICluster.Info">ICluster.Info</see>.
        /// </summary>
        /// <returns></returns>
        [Obsolete("Use IClusterManager.ClusterInfo() instead")]
        public IClusterInfo Info()
        {
            var httpProvider = ConfigProviders.Find(x => x is HttpStreamingProvider) as HttpStreamingProvider;
            if (httpProvider != null && httpProvider.GetCachedServerConfig() != null)
            {
                return new ClusterInfo(httpProvider.GetCachedServerConfig());
            }
            throw new InvalidOperationException("Cannot get Info if HttpProvider has not been initialized");
        }

        /// <summary>
        /// Returns a boolean indicating whether or not the bucket has been opened with this cluster instance.
        /// </summary>
        /// <param name="bucketName">The name of the bucket.</param>
        /// <returns>True if the bucket exists</returns>
        public bool IsObserving(string bucketName)
        {
            return _buckets.ContainsKey(bucketName);
        }

        public ClientConfiguration Configuration { get { return _clientConfig; } }

        public void CheckConfigUpdate(string bucketName, IPEndPoint excludeEndPoint)
        {
            var lockAcquired = false;
            try
            {
                Monitor.TryEnter(_syncObject, ref lockAcquired);
                var now = DateTime.Now;
                var lastCheckedPlus = LastConfigCheckedTime.AddMilliseconds(Configuration.HeartbeatConfigCheckFloor);
                if (!lockAcquired || lastCheckedPlus > now)
                {
                    Log.Info("Not checking config because {0} > {1} or a lock ({2}) could not be acquired.",
                        lastCheckedPlus, now, lockAcquired);
                    return;
                }

                Log.Info("Checking config because {0} < {1}", lastCheckedPlus, now);
                var provider = _configProviders.FirstOrDefault(x => x is CarrierPublicationProvider);
                if (provider != null)
                {
                    var configInfo = provider.GetCached(bucketName);
                    var servers = configInfo.Servers.
                        Where(x => x.IsDataNode && !x.IsDown && !x.EndPoint.Equals(excludeEndPoint)).
                        ToList().
                        Shuffle();

                    if (servers.Any())
                    {
                        var server = servers.First();

                        Log.Info("Checking for new config {0}", server.EndPoint);
                        var config = server.Send(
                            new Config(Transcoder, _clientConfig.DefaultOperationLifespan, server.EndPoint));

                        if (config.Success)
                        {
                            EnqueueConfigForProcessing(config.Value);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Info(e);
            }
            finally
            {
                LastConfigCheckedTime = DateTime.Now;
            }
            if (lockAcquired)
            {
                Monitor.Exit(_syncObject);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void Dispose(bool disposing)
        {
            lock (_syncObject)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        GC.SuppressFinalize(this);
                    }
                    if (_clusterMonitor != null)
                    {
                        _clusterMonitor.Dispose();
                    }
                    if (_configMonitor != null)
                    {
                        _configMonitor.Dispose();
                    }
                    foreach (var pair in _buckets)
                    {
                        DestroyBucket(pair.Value);
                    }
                    foreach (var configProvider in ConfigProviders)
                    {
                        configProvider.Dispose();
                    }
                    _disposed = true;
                }
            }
        }

#if DEBUG
        ~ClusterController()
        {
            Dispose(false);
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
