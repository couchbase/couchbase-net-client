using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using Couchbase.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;

namespace Couchbase.Configuration.Server.Providers
{
    internal abstract class ConfigProviderBase : IConfigProvider
    {
        protected readonly static ILog Log = LogManager.GetLogger<ConfigProviderBase>();
        private readonly ClientConfiguration _clientConfig;
        private readonly Func<string, string, IIOService, ITypeTranscoder, ISaslMechanism> _saslFactory;
        private readonly Func<IConnectionPool, IIOService> _ioServiceFactory;
        private readonly Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private readonly ConcurrentDictionary<string, IConfigInfo> _configs = new ConcurrentDictionary<string, IConfigInfo>();
        private readonly ConcurrentDictionary<string, IConfigObserver> _configObservers = new ConcurrentDictionary<string, IConfigObserver>();
        protected volatile bool Disposed;
        protected object SyncObj = new object();
        protected ReaderWriterLockSlim ConfigLock = new ReaderWriterLockSlim();

        protected ConfigProviderBase(ClientConfiguration clientConfig,
            Func<IConnectionPool, IIOService> ioServiceFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IIOService, ITypeTranscoder, ISaslMechanism> saslFactory,
            IByteConverter converter,
            ITypeTranscoder transcoder)
        {
            _clientConfig = clientConfig;
            _ioServiceFactory = ioServiceFactory;
            _connectionPoolFactory = connectionPoolFactory;
            _saslFactory = saslFactory;
            Converter = converter;
            Transcoder = transcoder;
        }

        protected ClientConfiguration ClientConfig
        {
            get { return _clientConfig; }
        }

        protected Func<string, string, IIOService, ITypeTranscoder, ISaslMechanism> SaslFactory
        {
            get { return _saslFactory; }
        }

        protected Func<IConnectionPool, IIOService> IOServiceFactory
        {
            get { return _ioServiceFactory; }
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

        public IByteConverter Converter { get; set; }

        public ITypeTranscoder Transcoder { get; set; }

        public abstract IConfigInfo GetConfig(string bucketName, string username, string password);

        public abstract bool RegisterObserver(IConfigObserver observer);

        public abstract void UnRegisterObserver(IConfigObserver observer);

        public abstract void Dispose();

        /// <summary>
        /// Gets an <see cref="BucketConfiguration"/> from the <see cref="ClientConfiguration"/>. If one doesn't exist
        /// for a given bucket, a new one will be created and added to the configuration.
        /// </summary>
        /// <param name="bucketName">The <see cref="IBucket.Name"/> to use for the lookup.</param>
        /// <returns>An <see cref="BucketConfiguration"/> instance.</returns>
        protected virtual BucketConfiguration GetOrCreateConfiguration(string bucketName)
        {
            try
            {
                ConfigLock.EnterWriteLock();
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
                        BucketName = bucketName,
                        PoolConfiguration = ClientConfig.PoolConfiguration,
                        Servers = ClientConfig.Servers,
                        UseSsl = ClientConfig.UseSsl
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
                        Password = string.Empty,
                        UseSsl = defaultConfig.UseSsl
                    };
                }
                ClientConfig.BucketConfigs.Add(bucketConfiguration.BucketName, bucketConfiguration);
                return bucketConfiguration;
            }
            finally
            {
                ConfigLock.ExitWriteLock();
            }
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
            return GetConfig(bucketName, string.Empty, string.Empty);
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

        /// <summary>
        /// Updates the new configuration if the new configuration revision is greater than the current configuration.
        /// </summary>
        /// <param name="bucketConfig">The bucket configuration.</param>
        /// <param name="force">if set to <c>true</c> [force].</param>
        public abstract void UpdateConfig(IBucketConfig bucketConfig, bool force = false);
    }
}
