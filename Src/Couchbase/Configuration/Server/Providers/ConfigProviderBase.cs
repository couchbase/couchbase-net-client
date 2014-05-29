using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;

namespace Couchbase.Configuration.Server.Providers
{
    internal abstract class ConfigProviderBase : IConfigProvider
    {
        protected readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ClientConfiguration _clientConfig;
        private readonly Func<string, string, IOStrategy, ISaslMechanism> _saslFactory;
        private readonly Func<IConnectionPool, ISaslMechanism, IOStrategy> _ioStrategyFactory;
        private readonly Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private readonly ConcurrentDictionary<string, IConfigInfo> _configs = new ConcurrentDictionary<string, IConfigInfo>();
        private readonly ConcurrentDictionary<string, IConfigObserver> _configObservers = new ConcurrentDictionary<string, IConfigObserver>();
        protected volatile bool Disposed;

        protected ConfigProviderBase(ClientConfiguration clientConfig,
            Func<IConnectionPool, ISaslMechanism, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IOStrategy, ISaslMechanism> saslFactory)
        {
            _clientConfig = clientConfig;
            _ioStrategyFactory = ioStrategyFactory;
            _connectionPoolFactory = connectionPoolFactory;
            _saslFactory = saslFactory;
        }

        protected ClientConfiguration ClientConfig
        {
            get { return _clientConfig; }
        }

        protected Func<string, string, IOStrategy, ISaslMechanism> SaslFactory
        {
            get { return _saslFactory; }
        }

        protected Func<IConnectionPool, ISaslMechanism, IOStrategy> IOStrategyFactory
        {
            get { return _ioStrategyFactory; }
        }

        protected Func<PoolConfiguration, IPEndPoint, IConnectionPool> ConnectionPoolFactory
        {
            get { return _connectionPoolFactory; }
        }

        protected ConcurrentDictionary<string, IConfigInfo> Configs
        {
            get { return _configs; }
        }

        protected ConcurrentDictionary<string, IConfigObserver> ConfigObservers
        {
            get { return _configObservers; }
        }

        public abstract IConfigInfo GetConfig(string name, string password);

        public abstract bool RegisterObserver(IConfigObserver observer);

        public abstract void UnRegisterObserver(IConfigObserver observer);

        public abstract void Dispose();

        protected virtual BucketConfiguration GetOrCreateConfiguration(string bucketName)
        {
            BucketConfiguration bucketConfiguration = null;
            if (ClientConfig.BucketConfigs.ContainsKey(bucketName))
            {
                bucketConfiguration = ClientConfig.BucketConfigs[bucketName];
            }
            if (bucketConfiguration != null) return bucketConfiguration;
            var defaultBucket = ClientConfig.BucketConfigs.FirstOrDefault();
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
                    Password = defaultConfig.Password,
                    EncryptTraffic = defaultConfig.EncryptTraffic
                };
            }
            ClientConfig.BucketConfigs.Add(bucketConfiguration.BucketName, bucketConfiguration);
            return bucketConfiguration;
        }

        /// <summary>
        /// Gets the currently cached (and used) configuration.
        /// </summary>
        /// <param name="bucketName">The name of the Couchbase Bucket used to lookup the <see cref="IConfigInfo"/> object.</param>
        /// <returns></returns>
        public virtual IConfigInfo GetCached(string bucketName)
        {
            IConfigInfo configInfo;
            if (!_configs.TryGetValue(bucketName, out configInfo))
            {
                throw new ConfigNotFoundException(bucketName);
            }
            return configInfo;
        }

        /// <summary>
        /// Starts the HTTP streaming connection to the Couchbase Server and gets the latest configuration for a non-SASL authenticated Bucket.
        /// </summary>
        /// <param name="bucketName">The name of the Couchbase Bucket.</param>
        /// <returns>A <see cref="IConfigInfo"/> object representing the latest configuration.</returns>
        public virtual IConfigInfo GetConfig(string bucketName)
        {
            return GetConfig(bucketName, string.Empty);
        }

        /// <summary>
        /// Checks to see if an observer has been registered.
        /// </summary>
        /// <param name="observer"></param>
        /// <returns></returns>
        public virtual bool ObserverExists(IConfigObserver observer)
        {
            return ConfigObservers.ContainsKey(observer.Name);
        }
    }
}
