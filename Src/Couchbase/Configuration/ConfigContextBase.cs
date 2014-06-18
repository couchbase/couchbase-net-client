using System.Threading;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO;
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
        protected IKeyMapper KeyMapper;
        private readonly DateTime _creationTime;
        private readonly ClientConfiguration _clientConfig;
        private readonly List<IServer> _servers = new List<IServer>();
        protected Func<IConnectionPool, IOStrategy> IOStrategyFactory;
        protected Func<PoolConfiguration, IPEndPoint, IConnectionPool> ConnectionPoolFactory;
        protected readonly Func<string, string, IOStrategy, IByteConverter, ISaslMechanism> SaslFactory;
        protected readonly IByteConverter Converter;
        private bool _disposed;

        protected ConfigContextBase(IBucketConfig bucketConfig, ClientConfiguration clientConfig,
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IOStrategy, IByteConverter, ISaslMechanism> saslFactory, 
            IByteConverter converter)
        {
            _clientConfig = clientConfig;
            IOStrategyFactory = ioStrategyFactory;
            ConnectionPoolFactory = connectionPoolFactory;
            _creationTime = DateTime.Now;
            SaslFactory = saslFactory;
            Converter = converter;
            LoadConfig(bucketConfig);
        }

        protected List<IServer> Servers
        {
            get { return _servers; }
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
        public IBucketConfig BucketConfig { get; set; }

        /// <summary>
        /// The name of the Bucket that this configuration represents.
        /// </summary>
        public string BucketName
        {
            get { return BucketConfig.Name; }
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
                if (!Enum.TryParse(BucketConfig.BucketType, true, out bucketType))
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
                if (!Enum.TryParse(BucketConfig.NodeLocator, true, out nodeLocator))
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

            var bucket = _clientConfig.BucketConfigs[bucketConfig.Name];
            if (bucket.UseSsl)
            {
                var splits = address.Split(':');
                address = string.Concat(splits[0], ":", bucket.Port);
            }
            return UriExtensions.GetEndPoint(address);
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
            Log.Info(m=>m("Getting KeyMapper for rev#{0} on thread {1}", BucketConfig.Rev, Thread.CurrentThread.ManagedThreadId));
            return KeyMapper;
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