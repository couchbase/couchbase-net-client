using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
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
using Couchbase.IO.Strategies;
using Couchbase.Utils;

namespace Couchbase.Core
{
    internal sealed class ClusterController : IClusterController
    {
        private static readonly ILogger Log = new LoggerFactory().CreateLogger<ClusterController>();
        private readonly ClientConfiguration _clientConfig;
        private readonly ConcurrentDictionary<string, IBucket> _buckets = new ConcurrentDictionary<string, IBucket>();
        private readonly ConcurrentDictionary<string, int> _refCount = new ConcurrentDictionary<string, int>();
        private readonly List<IConfigProvider> _configProviders = new List<IConfigProvider>();
        private readonly Func<IConnectionPool, IOStrategy> _ioStrategyFactory;
        private readonly Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private readonly Func<string, string, IOStrategy, ITypeTranscoder, ISaslMechanism> _saslFactory;
        private readonly object _syncObject = new object();
        private volatile bool _disposed;

        public ClusterController(ClientConfiguration clientConfig)
            : this(clientConfig,
                pool =>
                {
                    Log.Debug("Creating DefaultIOStrategy");
                    return new DefaultIOStrategy(pool);
                },
            (config, endpoint) =>
            {
                IConnectionPool connectionPool;
                if (config.UseSsl)
                {
                    connectionPool = new ConnectionPool<SslConnection>(config, endpoint);
                }
                else
                {
                    connectionPool = new ConnectionPool<Connection>(config, endpoint);
                }
                connectionPool.Initialize();
                return connectionPool;
            },
            SaslFactory.GetFactory(),
            clientConfig.Converter(),
            clientConfig.Transcoder())
        {
        }

        public ClusterController(ClientConfiguration clientConfig, Func<IConnectionPool, IOStrategy> ioStrategyFactory)
            : this(clientConfig,
            ioStrategyFactory,
            (config, endpoint) =>
            {
                IConnectionPool connectionPool;
                if (config.UseSsl)
                {
                    connectionPool = new ConnectionPool<SslConnection>(config, endpoint);
                }
                else
                {
                    connectionPool = new ConnectionPool<Connection>(config, endpoint);
                }
                connectionPool.Initialize();
                return connectionPool;
            }, SaslFactory.GetFactory(),
            clientConfig.Converter(),
            clientConfig.Transcoder())
        {
        }

        public ClusterController(ClientConfiguration clientConfig,
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IOStrategy, ITypeTranscoder, ISaslMechanism> saslFactory,
            IByteConverter converter,
            ITypeTranscoder transcoder)
        {
            _clientConfig = clientConfig;
            _ioStrategyFactory = ioStrategyFactory;
            _connectionPoolFactory = connectionPoolFactory;
            _saslFactory = saslFactory;
            Converter = converter;
            Transcoder = transcoder;
            Initialize();
        }

        public IByteConverter Converter { get; private set; }

        public ITypeTranscoder Transcoder { get; private set; }

        public List<IConfigProvider> ConfigProviders { get { return _configProviders; } }

        private void Initialize()
        {
            _clientConfig.Initialize();
            _configProviders.Add(new CarrierPublicationProvider(_clientConfig,
                _ioStrategyFactory,
                _connectionPoolFactory,
                _saslFactory,
                Converter,
                Transcoder));

            _configProviders.Add(new HttpStreamingProvider(_clientConfig,
                _ioStrategyFactory,
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
            var provider = _configProviders.FirstOrDefault(x => x is CarrierPublicationProvider);
            if (provider != null)
            {
                var carrierPublicationProvider = provider as CarrierPublicationProvider;
                if (carrierPublicationProvider != null)
                {
                    carrierPublicationProvider.UpdateConfig(bucketConfig, force);
                }
            }
        }

        public IBucket CreateBucket(string bucketName)
        {
            //try to find a password in configuration
            BucketConfiguration bucketConfig;
            if (_clientConfig.BucketConfigs.TryGetValue(bucketName, out bucketConfig)
                && bucketConfig.Password != null)
            {
                return CreateBucket(bucketName, bucketConfig.Password);
            }
            return CreateBucket(bucketName, string.Empty);
        }

        public IBucket CreateBucket(string bucketName, string password)
        {
            var exceptions = new List<Exception>();
            lock (_syncObject)
            {
                //shortcircuit in case lock was waited upon because another thread bootstraped same bucket
                if (_buckets.ContainsKey(bucketName))
                {
                    Log.Debug($"Bootstraping was already done, returning existing bucket {bucketName}");
                    return _buckets[bucketName];
                }
                //otherwise bootstrap a new bucket
                var success = false;
                IBucket bucket = null;
                foreach (var provider in _configProviders)
                {
                    try
                    {
                        Log.Debug($"Trying to bootstrap with {provider}.");
                        var config = provider.GetConfig(bucketName, password);
                        IRefCountable refCountable = null;
                        switch (config.NodeLocator)
                        {
                            case NodeLocatorEnum.VBucket:
                                bucket = _buckets.GetOrAdd(bucketName,
                                    name => new CouchbaseBucket(this, bucketName, Converter, Transcoder));
                                refCountable = bucket as IRefCountable;
                                if (refCountable != null)
                                {
                                    refCountable.AddRef();
                                }
                                break;

                            case NodeLocatorEnum.Ketama:
                                bucket = _buckets.GetOrAdd(bucketName,
                                    name => new MemcachedBucket(this, bucketName, Converter, Transcoder));
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
                            Log.Debug($"Using existing bootstrap {provider}.");
                            _clientConfig.UpdateBootstrapList(config.BucketConfig);

                            configObserver.NotifyConfigChanged(config);
                            success = true;
                            break;
                        }

                        if (provider.RegisterObserver(configObserver) &&
                            _buckets.TryAdd(bucket.Name, bucket))
                        {
                            Log.Debug($"Successfully bootstrapped using {provider}.");
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
