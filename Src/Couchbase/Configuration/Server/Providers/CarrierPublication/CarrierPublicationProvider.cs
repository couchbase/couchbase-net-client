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
    internal class CarrierPublicationProvider : IConfigProvider, IDisposable
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private IServerConfig _serverConfig;
        private readonly ClientConfiguration _clientConfig;
        private readonly ConcurrentDictionary<string, IConfigInfo> _configs = new ConcurrentDictionary<string, IConfigInfo>();
        private readonly ConcurrentDictionary<string, IConfigListener> _listeners = new ConcurrentDictionary<string, IConfigListener>();

        public CarrierPublicationProvider(ClientConfiguration clientConfig) 
        {
            _clientConfig = clientConfig;
        }

        public IConfigInfo GetCached()
        {
            throw new NotImplementedException();
        }

        public IConfigInfo GetConfig()
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            //throw new NotImplementedException();
        }

        public void RegisterListener(IConfigListener listener)
        {
            //pick a URI from the client configuration
            //create a connection to the node
            //use the bucket name to get the client configuration
            //provider needs to register as an observer of buckets, since a NMV
            //will generate a configuration which needs to be forwarded back up to the provider
            //which will raise the ConfigHandlerChanged event - the client will then re-configuration

            var bootstrap = _clientConfig.BucketConfigs.FirstOrDefault(x => x.BucketName == listener.Name);
            if (bootstrap == null)
            {
                throw new BucketNotFoundException(listener.Name);
            }

            var connectionPool = new DefaultConnectionPool(bootstrap.PoolConfiguration, bootstrap.GetEndPoint());
            var ioStrategy = new AwaitableIOStrategy(connectionPool, null);

            var task = ioStrategy.ExecuteAsync(new ConfigOperation());
            var operationResult = task.Result;
            if (operationResult.Success)
            {
                var bucketConfig = operationResult.Value;
                bucketConfig.SurrogateHost = connectionPool.EndPoint.Address.ToString();//for $HOST blah-ness
                var configInfo = new DefaultConfig(_clientConfig)
                {
                    BucketConfig = bucketConfig
                };
                _configs[listener.Name] = configInfo;
                listener.NotifyConfigChanged(configInfo, connectionPool);
            }
        }

        public void UpdateConfig(IBucketConfig bucketConfig)
        {
            //missing lots of validation here
            var listener = _listeners[bucketConfig.Name];
            var configInfo = new DefaultConfig(_clientConfig)
            {
                BucketConfig = bucketConfig
            };
            _configs[bucketConfig.Name] = configInfo;
            listener.NotifyConfigChanged(configInfo);
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
    }
}