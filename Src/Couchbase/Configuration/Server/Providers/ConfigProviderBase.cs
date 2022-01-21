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
        private readonly Func<string, string, IConnectionPool, ITypeTranscoder, ISaslMechanism> _saslFactory;
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
        }

        protected ClientConfiguration ClientConfig
        {
            get { return _clientConfig; }
        }

        protected Func<string, string, IConnectionPool, ITypeTranscoder, ISaslMechanism> SaslFactory
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

        internal string NetworkType { get; set; }

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

                // do we already have the bucket config cached?
                if (ClientConfig.BucketConfigs.TryGetValue(bucketName, out var bucketConfiguration))
                {
                    SetNetworkType(bucketConfiguration.NetworkType);
                    return bucketConfiguration;
                }

                // create new config using client configuration settings
                bucketConfiguration = new BucketConfiguration
                {
                    BucketName = bucketName,
                    PoolConfiguration = ClientConfig.PoolConfiguration,
                    Servers = ClientConfig.Servers,
                    UseSsl = ClientConfig.UseSsl,
                    Port = ClientConfig.DirectPort,
                    NetworkType = ClientConfig.NetworkType
                };

                SetNetworkType(bucketConfiguration.NetworkType);

                // cache bucket config
                ClientConfig.BucketConfigs.Add(bucketConfiguration.BucketName, bucketConfiguration);

                return bucketConfiguration;
            }
            finally
            {
                ConfigLock.ExitWriteLock();
            }
        }

        private void SetNetworkType(string network)
        {
            // only set if Network has not being set before
            if (string.IsNullOrWhiteSpace(NetworkType))
            {
                // default to 'auto' if network is empty
                NetworkType = string.IsNullOrWhiteSpace(network)
                    ? NetworkTypes.Auto
                    : network;

                Log.Info($"Using network type: '{NetworkType}'");
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
            return GetConfig(bucketName, bucketName, string.Empty);
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

        protected void LogServers(IConfigInfo config)
        {
            var servers = config.Servers.Select(x => x.EndPoint.ToString());
            Log.Debug($"Current cluster nodes: {servers}");
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
