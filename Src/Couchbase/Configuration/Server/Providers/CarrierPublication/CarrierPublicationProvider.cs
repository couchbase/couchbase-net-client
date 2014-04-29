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

namespace Couchbase.Configuration.Server.Providers.CarrierPublication
{
    internal sealed class CarrierPublicationProvider : IConfigProvider, IDisposable
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ClientConfiguration _clientConfig;
        private readonly Func<IConnectionPool, IOStrategy> _ioStrategyFactory;
        private readonly Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private readonly ConcurrentDictionary<string, IConfigInfo> _configs = new ConcurrentDictionary<string, IConfigInfo>();
        private readonly ConcurrentDictionary<string, IConfigObserver> _listeners = new ConcurrentDictionary<string, IConfigObserver>();

        public CarrierPublicationProvider(ClientConfiguration clientConfig)
        {
            _clientConfig = clientConfig;
        }

        public CarrierPublicationProvider(ClientConfiguration clientConfig,
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
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
            var bootstrap = _clientConfig.BucketConfigs.FirstOrDefault(x => x.BucketName == bucketName);
            if (bootstrap == null)
            {
                var defaultBucket = _clientConfig.BucketConfigs.FirstOrDefault();
                if (defaultBucket == null)
                {
                    bootstrap = new BucketConfiguration
                    {
                        BucketName = bucketName
                    };
                }
                else
                {
                    bootstrap = new BucketConfiguration
                    {
                        BucketName = bucketName,
                        PoolConfiguration = defaultBucket.PoolConfiguration,
                        Servers = defaultBucket.Servers,
                        Port = defaultBucket.Port,
                        Username = defaultBucket.Username,
                        Password = defaultBucket.Password
                    };
                }
                _clientConfig.BucketConfigs.Add(bootstrap);
            }

            var saslMechanism = new PlainTextMechanism(bucketName, password);
            var connectionPool = new DefaultConnectionPool(bootstrap.PoolConfiguration, bootstrap.GetEndPoint());
            var ioStrategy = new SocketAsyncStrategy(connectionPool, saslMechanism);//this needs to be configurable

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
            IConfigInfo configInfo = new CouchbaseConfigContext(bucketConfig,
                _clientConfig,
                _ioStrategyFactory,
                _connectionPoolFactory);

            return configInfo;
        }

        public bool RegisterObserver(IConfigObserver observer)
        {
            return _listeners.TryAdd(observer.Name, observer);
        }

        public void UpdateConfig(IBucketConfig bucketConfig)
        {
            IConfigObserver observer;
            if (!_listeners.TryGetValue(bucketConfig.Name, out observer))
            {
                throw new ConfigListenerNotFoundException(bucketConfig.Name);
            }

            IConfigInfo oldConfigInfo;
            if (!_configs.TryGetValue(bucketConfig.Name, out oldConfigInfo))
            {
                throw new ConfigNotFoundException(bucketConfig.Name);
            }

            var configInfo = GetConfig(bucketConfig);
            if (_configs.TryUpdate(bucketConfig.Name, configInfo, oldConfigInfo))
            {
                observer.NotifyConfigChanged(configInfo);
            }
        }

        public void UnRegisterObserver(IConfigObserver observer)
        {
            IConfigObserver observerToRemove;
            if (_listeners.TryRemove(observer.Name, out observerToRemove))
            {
                Log.Info(m => m("Unregistering observer {0}", observerToRemove.Name));

                IConfigInfo configInfo;
                if (_configs.TryRemove(observer.Name, out configInfo))
                {
                    Log.Info(m => m("Removing config for observer {0}", observer.Name));
                }
                else
                {
                    Log.Warn(m => m("Could not remove config for {0}", observer.Name));
                }
            }
            else
            {
                Log.Warn(m => m("Could not unregister observer {0}", observer.Name));
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool ObserverExists(IConfigObserver observer)
        {
            return _listeners.ContainsKey(observer.Name);
        }
    }
}