using System;
using System.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;
using Couchbase.Core;

namespace Couchbase
{
    /// <summary>
    /// The client interface to a Couchbase Server Cluster.
    /// </summary>
    public sealed class CouchbaseCluster : ICouchbaseCluster 
    {
        private const string DefaultBucket = "default";
        private static Lazy<CouchbaseCluster> _instance;
        private readonly ClientConfiguration _configuration;
        private readonly IClusterManager _clusterManager;
        private static readonly object SyncObj = new object();

        /// <summary>
        /// Ctor for creating Cluster instance.
        /// </summary>
        /// <remarks>
        /// This is the default configuration and will attempt to boostrap off of localhost.
        /// </remarks>
        private CouchbaseCluster() 
            : this(new ClientConfiguration())
        {
        }

        /// <summary>
        /// Ctor for creating Cluster instance. 
        /// </summary>
        /// <param name="configuration">The ClientCOnfiguration to use for initialization.</param>
        private CouchbaseCluster(ClientConfiguration configuration) 
            : this(configuration, new ClusterManager(configuration))
        {
        }

        /// <summary>
        /// Ctor for creating Cluster instance.
        /// </summary>
        /// <param name="configuration">The ClientCOnfiguration to use for initialization.</param>
        /// <param name="clusterManager">The ClusterManager instance use.</param>
        /// <remarks>
        /// This overload is primarly added for testing.
        /// </remarks>
        private CouchbaseCluster(ClientConfiguration configuration, IClusterManager clusterManager)
        {
            _configuration = configuration;
            _clusterManager = clusterManager;
        }

        /// <summary>
        /// Returns a Singleton instance of the Cluster class.
        /// </summary>
        /// <remarks>
        /// Call one of the Initialize() overloads to create or recreate the Singleton instance.
        /// However, Initialize() should only be called when the process starts up.
        /// </remarks>
        /// <returns>A Singleton instance of the Cluster class.</returns>
        /// <exception cref="Couchbase.Core.InitializationException">Thrown if Initialize is not called before accessing this method.</exception>
        public static CouchbaseCluster Get()
        {
            if (_instance == null)
            {
                throw new InitializationException("Call Cluster.Initialize() before calling this method.");
            }
            return _instance.Value;
        }

        /// <summary>
        /// Initializes the Cluster instance using a given factory Func.
        /// </summary>
        /// <remarks>
        /// Call this on the Cluster object before calling Get() to return an instance. Note that
        /// this method should only be called during application or process startup or in certain
        /// scenarios where you explicitly want to reinitialize the current cluster instance.
        /// </remarks>
        /// <param name="factory">The factory Func that creates the instance.</param>
        private static void Initialize(Func<CouchbaseCluster> factory)
        {
            lock (SyncObj)
            {
                if (_instance != null && _instance.IsValueCreated)
                {
                    var cluster = _instance.Value;
                    if (cluster != null)
                    {
                        cluster.Dispose();
                        _instance = null;
                    }
                }
                _instance = new Lazy<CouchbaseCluster>(factory);
            }
        }

        /// <summary>
        /// Initializes a new Cluster instance with a given ClientConfiguration and ClusterManager.
        /// This overload is primarily provided for testing given that it allows you to set the
        /// major dependencies of the Cluster class and it's scope is internal.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="clusterManager"></param>
        internal static void Initialize(ClientConfiguration configuration, IClusterManager clusterManager)
        {
            if (configuration == null || clusterManager == null)
            {
                throw new ArgumentNullException(configuration == null ? "configuration" : "clusterManager");
            }
            
            configuration.Initialize();
            var factory = new Func<CouchbaseCluster>(() => new CouchbaseCluster(configuration, clusterManager));
            Initialize(factory);
        }

        /// <summary>
        /// Creates a Cluster instance.
        /// </summary>
        /// <param name="configuration">
        /// The ClientConfiguration to use when initialize the internal ClusterManager
        /// </param>
        /// <remarks>
        /// This is an heavy-weight object intended to be long-lived. Create one per process or App.Domain.
        /// </remarks>
        public static void Initialize(ClientConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            configuration.Initialize();
            var factory = new Func<CouchbaseCluster>(() => new CouchbaseCluster(configuration, new ClusterManager(configuration)));
            Initialize(factory);
        }

        /// <summary>
        /// Creates a Cluster instance using the default configuration. This is overload is suitable
        /// for development only as it will use localhost (127.0.0.1) and the default Couchbase REST
        /// and Memcached ports. 
        /// <see cref="http://docs.couchbase.com/couchbase-manual-2.5/cb-install/#network-ports" />
        /// </summary>
        public static void Initialize()
        {
            var factory = new Func<CouchbaseCluster>(() => new CouchbaseCluster());
            Initialize(factory);
        }

        public static void Initialize(string configurationSectionName)
        {
            var configurationSection = (CouchbaseClientSection)ConfigurationManager.GetSection(configurationSectionName);
            var configuration = new ClientConfiguration(configurationSection);
            configuration.Initialize();

            var factory = new Func<CouchbaseCluster>(() => new CouchbaseCluster(configuration, new ClusterManager(configuration)));
            Initialize(factory);
        }

        /// <summary>
        /// Opens the default bucket associated with a Couchbase Cluster.
        /// </summary>
        /// <returns>An instance which implements the IBucket interface with the
        /// default buckets configuration.</returns>
        /// <remarks>Use Cluster.CloseBucket(bucket) to release resources associated with a Bucket.</remarks>
        public IBucket OpenBucket()
        {
            return _clusterManager.CreateBucket(DefaultBucket);
        }

        /// <summary>
        /// Creates a connection to a specific SASL authenticated Couchbase Bucket.
        /// </summary>
        /// <param name="bucketname">The Couchbase Bucket to connect to.</param>
        /// <param name="password">The SASL password to use.</param>
        /// <returns>An instance which implements the IBucket interface.</returns>
        /// <remarks>Use Cluster.CloseBucket(bucket) to release resources associated with a Bucket.</remarks>
        public IBucket OpenBucket(string bucketname, string password)
        {
            return _clusterManager.CreateBucket(bucketname, password);
        }

        /// <summary>
        /// Creates a connection to a non-SASL Couchbase bucket.
        /// </summary>
        /// <param name="bucketname">The Couchbase Bucket to connect to.</param>
        /// <returns>An instance which implements the IBucket interface.</returns>
        /// <remarks>
        /// Use Cluster.CloseBucket(bucket) to release resources associated with a Bucket.
        /// </remarks>
        public IBucket OpenBucket(string bucketname)
        {
            if (string.IsNullOrWhiteSpace(bucketname))
            {
                if (bucketname == null)
                {
                    throw new ArgumentNullException("bucketname");
                }
                throw new ArgumentException("bucketname cannot be null, empty or whitespace.");
            }
            return _clusterManager.CreateBucket(bucketname);
        }

        /// <summary>
        /// Closes and releases all resources associated with a Couchbase bucket.
        /// </summary>
        /// <param name="bucket">The Bucket to close.</param>
        public void CloseBucket(IBucket bucket)
        {
            if (bucket == null)
            {
                throw new ArgumentNullException("bucket");
            }
            _clusterManager.DestroyBucket(bucket);
        }

        /// <summary>
        /// Returns an object representing cluster status information.
        /// </summary>
        public IClusterInfo Info
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// The current client configuration being used by the <see cref="CouchbaseCluster"/> object.
        /// Set this by passing in a <see cref="ClientConfiguration"/> object into <see cref="Initialize(ClientConfiguration)" /> or by
        /// providing a <see cref="CouchbaseClientSection"/> in your App.config or Web.config and calling <see cref="Initialize(string)"/>
        /// </summary>
        public ClientConfiguration Configuration
        {
            //TODO returned cloned copy?
            get { return _configuration; }
        }

        /// <summary>
        /// Closes and releases all internal resources.
        /// </summary>
        public void Dispose()
        {
            //There is a bug here somewhere - note that when called this should close and cleanup _everything_
            //however, if you do not explicitly call Cluster.CloseBucket(bucket) in certain cases the HttpStreamingProvider
            //listener thread will hang indefinitly if Cluster.Dispose() is called. This is a definite bug that needs to be
            //resolved before developer preview.
            if (_clusterManager != null)
            {
                _clusterManager.Dispose();
            }
            _instance = null;
        }

        /// <summary>
        /// Cleans up any non-reclaimed resources.
        /// </summary>
        /// <remarks>will run if Dispose is not called on a Cluster instance.</remarks>
        ~CouchbaseCluster()
        {
            if (_clusterManager != null)
            {
                _clusterManager.Dispose();
            }
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