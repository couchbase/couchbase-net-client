using System;
using System.Configuration;
using System.Net.Http;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;
using Couchbase.Core;
using Couchbase.Management;
using Couchbase.Views;

namespace Couchbase
{
    /// <summary>
    /// The client interface to a Couchbase Server Cluster.
    /// </summary>
    public sealed class CouchbaseCluster : ICouchbaseCluster
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private const string DefaultBucket = "default";
        private readonly ClientConfiguration _configuration;
        private readonly IClusterController _clusterController;

        /// <summary>
        /// Ctor for creating Cluster instance using the default settings.
        /// </summary>
        /// <remarks>
        /// This is the default configuration and will attempt to bootstrap off of localhost.
        /// </remarks>
        public CouchbaseCluster()
            : this(new ClientConfiguration())
        {
        }

        /// <summary>
        /// Ctor for creating Cluster instance using an App.Config or Web.config.
        /// </summary>
        /// <param name="configurationSectionName">The name of the configuration section to use.</param>
        /// <remarks>Note that <see cref="CouchbaseClientSection"/> needs include the sectionGroup name as well: "couchbaseSection/couchbase" </remarks>
        public CouchbaseCluster(string configurationSectionName)
            : this(new ClientConfiguration((CouchbaseClientSection)ConfigurationManager.GetSection(configurationSectionName)))
        {
        }

        /// <summary>
        /// Ctor for creating Cluster instance with a custom <see cref="ClientConfiguration"/> configuration.
        /// </summary>
        /// <param name="configuration">The ClientCOnfiguration to use for initialization.</param>
        public CouchbaseCluster(ClientConfiguration configuration)
            : this(configuration, new ClusterController(configuration))
        {
        }

        /// <summary>
        /// Ctor for creating Cluster instance.
        /// </summary>
        /// <param name="configuration">The ClientCOnfiguration to use for initialization.</param>
        /// <param name="clusterController">The ClusterManager instance use.</param>
        /// <remarks>
        /// This overload is primarly added for testing.
        /// </remarks>
        internal CouchbaseCluster(ClientConfiguration configuration, IClusterController clusterController)
        {
            _configuration = configuration;
            _clusterController = clusterController;
        }

        /// <summary>
        /// Opens the default bucket associated with a Couchbase Cluster.
        /// </summary>
        /// <returns>An instance which implements the IBucket interface with the
        /// default buckets configuration.</returns>
        /// <remarks>Use Cluster.CloseBucket(bucket) to release resources associated with a Bucket.</remarks>
        public IBucket OpenBucket()
        {
            return _clusterController.CreateBucket(DefaultBucket);
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
            return _clusterController.CreateBucket(bucketname, password);
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
            return _clusterController.CreateBucket(bucketname);
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
            _clusterController.DestroyBucket(bucket);
        }

        /// <summary>
        /// Creates a <see cref="IClusterManager"/> object that uses the current <see cref="ICouchbaseCluster"/> configuration settings.
        /// </summary>
        /// <returns>A <see cref="IClusterManager"/> instance that uses the current <see cref="ICouchbaseCluster"/> configuration settings. </returns>
        public IClusterManager CreateManager(string username, string password)
        {
            return new ClusterManager(Configuration, _clusterController, new HttpClient(), new JsonDataMapper(), username, password);
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
            if (_clusterController != null)
            {
                Log.Debug(m => m("Disposing {0}", GetType().Name));
                _clusterController.Dispose();
            }
        }

        /// <summary>
        /// Cleans up any non-reclaimed resources.
        /// </summary>
        /// <remarks>will run if Dispose is not called on a Cluster instance.</remarks>
        ~CouchbaseCluster()
        {
            Log.Debug(m=>m("Finalizing {0}", GetType().Name));
            if (_clusterController != null)
            {
                _clusterController.Dispose();
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