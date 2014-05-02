using System.Threading;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.IO.Strategies.Async;
using Couchbase.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Couchbase.Configuration
{
    /// <summary>
    /// Base class for configuration contexts. The configuration context is a class which maintains the internal
    /// state of the cluster and communicats with configuration providers to ensure that the state is up-to-date.
    /// </summary>
    internal abstract class ConfigContextBase : IConfigInfo
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        protected IBucketConfig _bucketConfig;
        protected IKeyMapper _keyMapper;
        private readonly DateTime _creationTime;
        protected ClientConfiguration _clientConfig;
        protected readonly List<IServer> _servers = new List<IServer>();
        protected Func<IConnectionPool, IOStrategy> _ioStrategyFactory;
        protected Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private bool _disposed;

        protected ConfigContextBase(IBucketConfig bucketConfig, ClientConfiguration clientConfig)
            : this(bucketConfig, clientConfig, pool => new SocketAsyncStrategy(pool, new PlainTextMechanism(bucketConfig.Name, string.Empty)),
                (config, endpoint) => new DefaultConnectionPool(config, endpoint))
        {
        }

        protected ConfigContextBase(IBucketConfig bucketConfig, ClientConfiguration clientConfig,
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory)
        {
            _clientConfig = clientConfig;
            _ioStrategyFactory = ioStrategyFactory;
            _connectionPoolFactory = connectionPoolFactory;
            _creationTime = DateTime.Now;
            _bucketConfig = bucketConfig;
        }

        /// <summary>
        /// The time at which this configuration context has been created.
        /// </summary>
        public DateTime CreationTime
        {
            get { return _creationTime; }
        }

        /// <summary>
        /// The client configuration for a bucket.
        /// <remarks> See <see cref="IBucketConfig"/> for details.</remarks>
        /// </summary>
        public IBucketConfig BucketConfig
        {
            get { return _bucketConfig; }
        }

        /// <summary>
        /// The name of the Bucket that this configuration represents.
        /// </summary>
        public string BucketName
        {
            get { return _bucketConfig.Name; }
        }

        /// <summary>
        /// The client configuration.
        /// </summary>
        public ClientConfiguration ClientConfig
        {
            get { return _clientConfig; }
        }

        /// <summary>
        /// The <see cref="BucketTypeEnum"/> that this configuration context is for.
        /// </summary>
        public BucketTypeEnum BucketType
        {
            get
            {
                BucketTypeEnum bucketType;
                if (!Enum.TryParse(_bucketConfig.BucketType, true, out bucketType))
                {
                    throw new NullConfigException("BucketType is not defined");
                }
                return bucketType;
            }
        }

        /// <summary>
        /// The <see cref="NodeLocatorEnum"/> that this configuration is using.
        /// </summary>
        public NodeLocatorEnum NodeLocator
        {
            get
            {
                NodeLocatorEnum nodeLocator;
                if (!Enum.TryParse(_bucketConfig.NodeLocator, true, out nodeLocator))
                {
                    throw new NullConfigException("NodeLocator is not defined");
                }
                return nodeLocator;
            }
        }

        /// <summary>
        /// Gets an <see cref="IPEndPoint"/> for a given hostname and bucketconfig.
        /// </summary>
        /// <param name="hostName">The specified hostname.</param>
        /// <param name="bucketConfig">The <see cref="IBucketConfig"/> to use if replacement is required.</param>
        /// <returns></returns>
        protected virtual IPEndPoint GetEndPoint(string hostName, IBucketConfig bucketConfig)
        {
            const string blah = "$HOST";
            var address = hostName.Replace(blah, bucketConfig.SurrogateHost);
            return Core.Server.GetEndPoint(address);
        }

        /// <summary>
        /// Loads the most updated configuration creating any resources as needed.
        /// </summary>
        /// <param name="bucketConfig">The latest <see cref="IBucketConfig"/> 
        /// that will drive the recreation if the configuration context.</param>
        public abstract void LoadConfig(IBucketConfig bucketConfig);

        /// <summary>
        /// Gets the <see cref="IKeyMapper"/> instance associated with this <see cref="IConfigInfo"/>.
        /// </summary>
        /// <param name="bucketName"></param>
        /// <returns></returns>
        public IKeyMapper GetKeyMapper(string bucketName)
        {
            return _keyMapper;
        }

        /// <summary>
        /// Gets a random server instance from the underlying <see cref="IServer"/> collection.
        /// </summary>
        /// <returns></returns>
        public IServer GetServer()
        {
            return _servers.Shuffle().First();
        }

        /// <summary>
        /// Reclaims all resources and suppresses finalization.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Reclams all resources and optionally suppresses finalization.
        /// </summary>
        /// <param name="disposing">True to suppress finalization.</param>
        private void Dispose(bool disposing)
        {
            if (!_disposed) return;
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
            if (_servers != null)
            {
                _servers.ForEach(x => x.Dispose());
            }
            _disposed = false;
        }

        /// <summary>
        /// Reclaims all un-reclaimed resources.
        /// </summary>
        ~ConfigContextBase()
        {
            Dispose(false);
        }
    }
}