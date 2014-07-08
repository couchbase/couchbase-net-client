using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Serializers;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using System;
using System.Net;

namespace Couchbase.Configuration.Server.Providers.CarrierPublication
{
    internal sealed class CarrierPublicationProvider : ConfigProviderBase
    {
        public CarrierPublicationProvider(ClientConfiguration clientConfig,
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IOStrategy, IByteConverter, ISaslMechanism> saslFactory, 
            IByteConverter converter,
            ITypeSerializer serializer) 
            : base(clientConfig, ioStrategyFactory, connectionPoolFactory, saslFactory, converter, serializer)
        {
        }

        public override IConfigInfo GetConfig(string bucketName, string password)
        {
            var bucketConfiguration = GetOrCreateConfiguration(bucketName);
            password = string.IsNullOrEmpty(password) ? bucketConfiguration.Password : password;
            var connectionPool = ConnectionPoolFactory(bucketConfiguration.PoolConfiguration, bucketConfiguration.GetEndPoint());
            var ioStrategy = IOStrategyFactory(connectionPool);
            var saslMechanism = SaslFactory(bucketName, password, ioStrategy, Converter);
            ioStrategy.SaslMechanism = saslMechanism;

            IConfigInfo configInfo = null;
            var operationResult = ioStrategy.Execute(new Config(Converter));
            if (operationResult.Success)
            {
                var bucketConfig = operationResult.Value;
                bucketConfig.SurrogateHost = connectionPool.EndPoint.Address.ToString(); //for $HOST blah-ness

                configInfo = GetConfig(bucketConfig);
                Configs[bucketName] = configInfo;
            }
            else
            {
                if (operationResult.Status == ResponseStatus.UnknownCommand)
                {
                    throw new ConfigException("{0} is this a Memcached bucket?", operationResult.Value);
                }
                throw new ConfigException("Could not retrieve configuration for {0}. Reason: {1}", bucketName, operationResult.Message);
            }
            return configInfo;
        }

        private IConfigInfo GetConfig(IBucketConfig bucketConfig)
        {
            ConfigContextBase configInfo = new CouchbaseConfigContext(bucketConfig,
                ClientConfig,
                IOStrategyFactory,
                ConnectionPoolFactory, 
                SaslFactory,
                Converter,
                Serializer);

            return configInfo;
        }

        public override bool RegisterObserver(IConfigObserver observer)
        {
            return ConfigObservers.TryAdd(observer.Name, observer);
        }

        public void UpdateConfig(IBucketConfig bucketConfig)
        {
            IConfigObserver configObserver;
            if (!ConfigObservers.TryGetValue(bucketConfig.Name, out configObserver))
            {
                throw new ConfigObserverNotFoundException(bucketConfig.Name);
            }

            IConfigInfo oldConfigInfo;
            if (!Configs.TryGetValue(bucketConfig.Name, out oldConfigInfo))
            {
                throw new ConfigNotFoundException(bucketConfig.Name);
            }

            var oldBucketConfig = oldConfigInfo.BucketConfig;
            if (bucketConfig.Rev > oldBucketConfig.Rev)
            {
                var configInfo = GetConfig(bucketConfig);
                if (Configs.TryUpdate(bucketConfig.Name, configInfo, oldConfigInfo))
                {
                    configObserver.NotifyConfigChanged(configInfo);
                }
            }
        }

        public override void UnRegisterObserver(IConfigObserver observer)
        {
            IConfigObserver observerToRemove;
            if (ConfigObservers.TryRemove(observer.Name, out observerToRemove))
            {
                var temp = observerToRemove;
                Log.Info(m => m("Unregistering observer {0}", temp.Name));

                IConfigInfo configInfo;
                if (Configs.TryRemove(observer.Name, out configInfo))
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

        public override void Dispose()
        {
            Dispose(true);
        }

        public void Dispose(bool disposing)
        {
            if (!Disposed && disposing)
            {
                GC.SuppressFinalize(this);
            }
            foreach (var configObserver in ConfigObservers)
            {
                UnRegisterObserver(configObserver.Value);
            }
            ConfigObservers.Clear();
            Disposed = true;
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