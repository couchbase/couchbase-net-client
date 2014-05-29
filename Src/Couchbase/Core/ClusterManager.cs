using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.IO.Strategies;
using Couchbase.IO.Strategies.Async;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;

namespace Couchbase.Core
{
    internal sealed class ClusterManager : IClusterManager
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ClientConfiguration _clientConfig;
        private readonly ConcurrentDictionary<string, IBucket> _buckets = new ConcurrentDictionary<string, IBucket>();
        private readonly List<IConfigProvider> _configProviders = new List<IConfigProvider>();
        private readonly Func<IConnectionPool, ISaslMechanism, IOStrategy> _ioStrategyFactory;
        private readonly Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private readonly Func<string, string, IOStrategy, ISaslMechanism> _saslFactory;
        private bool _disposed;

        public ClusterManager(ClientConfiguration clientConfig)
            : this(clientConfig,
            (pool, sm) => new DefaultIOStrategy(pool, sm),
            (config, endpoint) =>
            {
                IConnectionPool connectionPool = null;
                if (config.EncryptTraffic)
                {
                    connectionPool = new ConnectionPool<SslConnection>(config, endpoint);
                }
                else
                {
                    connectionPool = new ConnectionPool<EapConnection>(config, endpoint);
                }
                return connectionPool;
            },
            SaslFactory.GetFactory3())
        {
        }

        public ClusterManager(ClientConfiguration clientConfig, Func<IConnectionPool, ISaslMechanism, IOStrategy> ioStrategyFactory)
            : this(clientConfig,
            ioStrategyFactory,
            (config, endpoint) =>
            {
                IConnectionPool connectionPool = null;
                if (config.EncryptTraffic)
                {
                    connectionPool = new ConnectionPool<SslConnection>(config, endpoint);
                }
                else
                {
                    connectionPool = new ConnectionPool<EapConnection>(config, endpoint);
                }
                return connectionPool;
            }, SaslFactory.GetFactory3())
        {
        }

        public ClusterManager(ClientConfiguration clientConfig, Func<IConnectionPool, ISaslMechanism, IOStrategy> ioStrategyFactory, Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory, Func<string, string, IOStrategy, ISaslMechanism> saslFactory)
        {
            _clientConfig = clientConfig;
            _ioStrategyFactory = ioStrategyFactory;
            _connectionPoolFactory = connectionPoolFactory;
            _saslFactory = saslFactory;
            Initialize();
        }

        public List<IConfigProvider> ConfigProviders { get { return _configProviders; } }

        //TODO possibly make providers instantiation configurable...maybe. perhaps.
        private void Initialize()
        {
            //_configProviders.Add(new CarrierPublicationProvider(_clientConfig, _ioStrategyFactory, _connectionPoolFactory, _saslFactory));
            _configProviders.Add(new HttpStreamingProvider(_clientConfig, _ioStrategyFactory, _connectionPoolFactory, _saslFactory));
        }

        public IConfigProvider GetProvider(string name)
        {
            throw new NotImplementedException();
        }

        public IBucket CreateBucket(string bucketName)
        {
            return CreateBucket(bucketName, string.Empty);
        }

        public IBucket CreateBucket(string bucketName, string password)
        {
            //note we probably want to treat this whole process as an aggregate and not log
            //until the process has completed with success or failure then logged along with
            //the sequence of events that occurred.
            var success = false;
            IBucket bucket = null;
            foreach (var provider in _configProviders)
            {
                try
                {
                    Log.DebugFormat("Trying to boostrap with {0}.", provider);
                    var config = provider.GetConfig(bucketName, password);
                    switch (config.NodeLocator)
                    {
                        case NodeLocatorEnum.VBucket:
                            bucket = new CouchbaseBucket(this, bucketName);
                            break;

                        case NodeLocatorEnum.Ketama:
                            bucket = new MemcachedBucket(this, bucketName);
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    var configObserver = bucket as IConfigObserver;
                    if (provider.ObserverExists(configObserver))
                    {
                        Log.DebugFormat("Using existing bootstrap {0}.", provider);
                        configObserver.NotifyConfigChanged(config);
                        success = true;
                        break;
                    }
                    if (provider.RegisterObserver(configObserver) &&
                        _buckets.TryAdd(bucket.Name, bucket))
                    {
                        Log.DebugFormat("Successfully boostrap using {0}.", provider);
                        configObserver.NotifyConfigChanged(config);
                        success = true;
                        break;
                    }
                }
                catch (BucketNotFoundException e)
                {
                    Log.Warn(e);
                }
                catch (ConfigException e)
                {
                    Log.Warn(e);
                }
                catch (AuthenticationException e)
                {
                    Log.Warn(e);
                    break;
                }
            }

            if (!success)
            {
                throw new ConfigException("Could not bootstrap {0}. See log for details.", bucketName);
            }
            return bucket;
        }

        public void NotifyConfigPublished(IBucketConfig bucketConfig)
        {
            var provider = _configProviders.FirstOrDefault(x => x is CarrierPublicationProvider);
            if (provider != null)
            {
                var carrierPublicationProvider = provider as CarrierPublicationProvider;
                if (carrierPublicationProvider != null)
                {
                    carrierPublicationProvider.UpdateConfig(bucketConfig);
                }
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

        public void Dispose()
        {
            Dispose(true);
        }

        public void Dispose(bool disposing)
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
                _disposed = true;
            }
        }

        ~ClusterManager()
        {
            Dispose(false);
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
