using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Async;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using Couchbase.Views;

namespace Couchbase.Configuration.Server.Providers.CarrierPublication
{
    internal sealed class CarrierPublicationProvider : IConfigProvider
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ClientConfiguration _clientConfig;
        private readonly Func<IConnectionPool, ISaslMechanism, IOStrategy> _ioStrategyFactory;
        private readonly Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private readonly ConcurrentDictionary<string, IConfigInfo> _configs = new ConcurrentDictionary<string, IConfigInfo>();
        private readonly ConcurrentDictionary<string, IConfigObserver> _configObservers = new ConcurrentDictionary<string, IConfigObserver>();
        private volatile bool _disposed;

        public CarrierPublicationProvider(ClientConfiguration clientConfig)
        {
            _clientConfig = clientConfig;
        }

        public CarrierPublicationProvider(ClientConfiguration clientConfig,
            Func<IConnectionPool, ISaslMechanism, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory)
        {
            _clientConfig = clientConfig;
            _ioStrategyFactory = ioStrategyFactory;
            _connectionPoolFactory = connectionPoolFactory;
        }

        public IConfigInfo GetCached(string bucketName)
        {
            IConfigInfo configInfo;
            if (!_configs.TryGetValue(bucketName, out configInfo))
            {
                throw new ConfigNotFoundException(bucketName);
            }
            return configInfo;
        }

        public IConfigInfo GetConfig(string bucketName, string password)
        {
            BucketConfiguration bucketConfiguration = null;
            if (_clientConfig.BucketConfigs.ContainsKey(bucketName))
            {
                bucketConfiguration = _clientConfig.BucketConfigs[bucketName];
            }
            if (bucketConfiguration == null)
            {
                var defaultBucket = _clientConfig.BucketConfigs.FirstOrDefault();
                if (defaultBucket.Value == null)
                {
                    bucketConfiguration = new BucketConfiguration
                    {
                        BucketName = bucketName
                    };
                }
                else
                {
                    var defaultConfig = defaultBucket.Value;
                    bucketConfiguration = new BucketConfiguration
                    {
                        BucketName = bucketName,
                        PoolConfiguration = defaultConfig.PoolConfiguration,
                        Servers = defaultConfig.Servers,
                        Port = defaultConfig.Port,
                        Username = defaultConfig.Username,
                        Password = defaultConfig.Password
                    };
                }
                _clientConfig.BucketConfigs.Add(bucketConfiguration.BucketName, bucketConfiguration);
            }

            var saslMechanism = new PlainTextMechanism(bucketName, password);
            var connectionPool = _connectionPoolFactory(bucketConfiguration.PoolConfiguration,
                bucketConfiguration.GetEndPoint());
            var ioStrategy = _ioStrategyFactory(connectionPool, saslMechanism);

            IConfigInfo configInfo = null;
            var operationResult = ioStrategy.Execute(new ConfigOperation());
            if (operationResult.Success)
            {
                var bucketConfig = operationResult.Value;
                bucketConfig.SurrogateHost = connectionPool.EndPoint.Address.ToString(); //for $HOST blah-ness

                configInfo = GetConfig(bucketConfig);
                _configs[bucketName] = configInfo;
            }
            else
            {
                if (operationResult.Status == ResponseStatus.UnknownCommand)
                {
                    throw new ConfigException("{0} is this a Memcached bucket?", operationResult.Value);
                }
            }

            return configInfo;
        }

        public IConfigInfo GetConfig(string bucketName)
        {
            return GetConfig(bucketName, string.Empty);
        }

        private IConfigInfo GetConfig(IBucketConfig bucketConfig)
        {
            ConfigContextBase configInfo = new CouchbaseConfigContext(bucketConfig,
                _clientConfig,
                _ioStrategyFactory,
                _connectionPoolFactory);

            return configInfo;
        }

        public bool RegisterObserver(IConfigObserver observer)
        {
            return _configObservers.TryAdd(observer.Name, observer);
        }

        public void UpdateConfig(IBucketConfig bucketConfig)
        {
            IConfigObserver configObserver;
            if (!_configObservers.TryGetValue(bucketConfig.Name, out configObserver))
            {
                throw new ConfigObserverNotFoundException(bucketConfig.Name);
            }

            IConfigInfo oldConfigInfo;
            if (!_configs.TryGetValue(bucketConfig.Name, out oldConfigInfo))
            {
                throw new ConfigNotFoundException(bucketConfig.Name);
            }

            var oldBucketConfig = oldConfigInfo.BucketConfig;
            if (bucketConfig.Rev > oldBucketConfig.Rev)
            {
                var configInfo = GetConfig(bucketConfig);
                if (_configs.TryUpdate(bucketConfig.Name, configInfo, oldConfigInfo))
                {
                    configObserver.NotifyConfigChanged(configInfo);
                }
            }
        }

        public bool ObserverExists(IConfigObserver observer)
        {
            return _configObservers.ContainsKey(observer.Name);
        }

        public void UnRegisterObserver(IConfigObserver observer)
        {
            IConfigObserver observerToRemove;
            if (_configObservers.TryRemove(observer.Name, out observerToRemove))
            {
                var temp = observerToRemove;
                Log.Info(m => m("Unregistering observer {0}", temp.Name));

                IConfigInfo configInfo;
                if (_configs.TryRemove(observer.Name, out configInfo))
                {
                    Log.Info(m => m("Removing config for observer {0}", observer.Name));
                }
                else
                {
                    Log.Warn(m => m("Could not remove config for {0}", observer.Name));
                }
                observerToRemove.Dispose();
            }
            else
            {
                Log.Warn(m => m("Could not unregister observer {0}", observer.Name));
            }
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
            foreach (var configObserver in _configObservers)
            {
                UnRegisterObserver(configObserver.Value);
            }
            _configObservers.Clear();
            _disposed = true;
        }

        ~CarrierPublicationProvider()
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