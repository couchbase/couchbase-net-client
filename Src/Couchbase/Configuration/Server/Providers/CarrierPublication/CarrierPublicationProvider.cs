using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Net;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.Utils;

namespace Couchbase.Configuration.Server.Providers.CarrierPublication
{
    internal sealed class CarrierPublicationProvider : IConfigProvider, IDisposable
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ClientConfiguration _clientConfig;
        private readonly Func<IConnectionPool, IOStrategy> _ioStrategyFactory;
        private readonly Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private readonly ConcurrentDictionary<string, IConfigInfo> _configs = new ConcurrentDictionary<string, IConfigInfo>();
        private readonly ConcurrentDictionary<string, IConfigListener> _listeners = new ConcurrentDictionary<string, IConfigListener>();

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

        public IConfigInfo GetConfig(string bucketName)
        {
            var bootstrap = _clientConfig.BucketConfigs.FirstOrDefault(x => x.BucketName == bucketName);
            if (bootstrap == null)
            {
                throw new BucketNotFoundException(bucketName);
            }

            var connectionPool = new DefaultConnectionPool(bootstrap.PoolConfiguration, bootstrap.GetEndPoint());
            var ioStrategy = new AwaitableIOStrategy(connectionPool, null);
            var task = ioStrategy.ExecuteAsync(new ConfigOperation());

            IConfigInfo configInfo = null;

            try
            {
                task.Wait();
                var operationResult = task.Result;
                if (operationResult.Success)
                {
                    var bucketConfig = operationResult.Value;
                    bucketConfig.SurrogateHost = connectionPool.EndPoint.Address.ToString(); //for $HOST blah-ness

                    configInfo = GetConfig(bucketConfig);
                    _configs[bucketName] = configInfo;
                }
            } 
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    Log.Error(e);
                    return true;
                });
            }
            return configInfo;
        }

        IConfigInfo GetConfig(IBucketConfig bucketConfig)
        {
            IConfigInfo configInfo = new CouchbaseConfigContext(bucketConfig, 
                _clientConfig, 
                _ioStrategyFactory, 
                _connectionPoolFactory);

            return configInfo;
        }

        public void Start()
        {
            //noop since cccp is demand based
        }

        public bool RegisterListener(IConfigListener listener)
        {
            return _listeners.TryAdd(listener.Name, listener);
        }

        public void UpdateConfig(IBucketConfig bucketConfig)
        {
            IConfigListener listener;
            if (!_listeners.TryGetValue(bucketConfig.Name, out listener))
            {
                throw new ConfigListenerNotFoundException(bucketConfig.Name);
            }

            IConfigInfo oldConfigInfo;
            if (!_configs.TryGetValue(bucketConfig.Name, out oldConfigInfo))
            {
                throw new ConfigNotFoundException(bucketConfig.Name);
            }

            var configInfo = GetConfig(bucketConfig);
            if(_configs.TryUpdate(bucketConfig.Name, configInfo, oldConfigInfo))
            {
                listener.NotifyConfigChanged(configInfo);
            }
        }

        public void UnRegisterListener(IConfigListener listener)
        {
            IConfigListener listenerToRemove;
            if (_listeners.TryRemove(listener.Name, out listenerToRemove))
            {
                Log.Info(m=>m("Unregistering listener {0}", listenerToRemove.Name));

                IConfigInfo configInfo;
                if(_configs.TryRemove(listener.Name, out configInfo))
                {
                    Log.Info(m=>m("Removing config for listener {0}", listener.Name));
                }
                else
                {
                    Log.Warn(m=>m("Could not remove config for {0}", listener.Name));
                }
            }
            else
            {
                Log.Warn(m=>m("Could not unregister listener {0}", listener.Name));
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool ListenerExists(IConfigListener listener)
        {
           return _listeners.ContainsKey(listener.Name);
        }
    }
}