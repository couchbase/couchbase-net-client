using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;
using Couchbase.Core;

namespace Couchbase
{
    /// <summary>
    /// A helper object for working with a <see cref="CouchbaseCluster"/> instance. 
    /// </summary>
    /// <remarks>Creates a singleton instance of a <see cref="CouchbaseCluster"/> object.</remarks>
    /// <remarks>Call <see cref="Initialize()"/> before calling <see cref="Get()"/> to get the instance.</remarks>
    public class ClusterHelper
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
        /// This is the default configuration and will attempt to bootstrap off of localhost.
        /// </remarks>
        public ClusterHelper() 
            : this(new ClientConfiguration())
        {
        }

        /// <summary>
        /// Ctor for creating Cluster instance. 
        /// </summary>
        /// <param name="configuration">The ClientCOnfiguration to use for initialization.</param>
        public ClusterHelper(ClientConfiguration configuration) 
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
        internal ClusterHelper(ClientConfiguration configuration, IClusterManager clusterManager)
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

        /// <summary>
        /// Ctor for creating Cluster instance.
        /// </summary>
        /// <param name="configurationSectionName">The name of the configuration section to use.</param>
        /// <remarks>Note that <see cref="CouchbaseClientSection"/> needs include the sectionGroup name as well: "couchbaseSection/couchbase" </remarks>
        public static void Initialize(string configurationSectionName)
        {
            var configurationSection = (CouchbaseClientSection)ConfigurationManager.GetSection(configurationSectionName);
            var configuration = new ClientConfiguration(configurationSection);
            configuration.Initialize();

            var factory = new Func<CouchbaseCluster>(() => new CouchbaseCluster(configuration, new ClusterManager(configuration)));
            Initialize(factory);
        }

        /// <summary>
        /// Disposes the current <see cref="CouchbaseCluster"/> instance and cleans up resources.
        /// </summary>
        public static void Close()
        {
            lock (SyncObj)
            {
                if (_instance == null || !_instance.IsValueCreated) return;
                var cluster = _instance.Value;
                if (cluster == null) return;
                cluster.Dispose();
                _instance = null;
            }
        }
    }
}

#region [ License information ]

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
