using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using System;
using System.Net;

namespace Couchbase
{
    /// <summary>
    /// The client interface to a Couchbase Server Cluster. 
    /// </summary>
    public sealed class Cluster3 : ICluster
    {
        private readonly IClusterManager _clusterManager;
        private readonly ClientConfiguration _config;

        internal Cluster3(ClientConfiguration config, Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory)
        {
            _config = config;
            _clusterManager = new ClusterManager(_config, ioStrategyFactory, connectionPoolFactory);
        }

        internal Cluster3(ClientConfiguration config, Func<IConnectionPool, IOStrategy> ioStrategyFactory)
        {
            _config = config;
            _clusterManager = new ClusterManager(_config, ioStrategyFactory);
        }

        /// <summary>
        /// Creates a Cluster instance.
        /// </summary>
        /// <param name="config">The ClientConfiguration to use when initialize the internal ClusterManager</param>
        ///<remarks>This is an heavy-weight object intended to be long-lived. Create one per process or App.Domain.</remarks>
        public Cluster3(ClientConfiguration config)
        {
            _config = config;
            _clusterManager = new ClusterManager(_config);
        }

        /// <summary>
        /// Creates a Cluster instance using the default configuration. This is overload is suitable for development only 
        /// as it will use localhost (127.0.0.1) and the default Couchbase REST and Memcached ports. 
        /// <see cref="http://docs.couchbase.com/couchbase-manual-2.5/cb-install/#network-ports"/>
        /// </summary>
        public Cluster()
            : this(new ClientConfiguration())
        {
        }

        /// <summary>
        /// Creates a connection to a specific SASL authenticated Couchbase Bucket.
        /// </summary>
        /// <param name="bucketName">The Couchbase Bucket to connect to.</param>
        /// <param name="password">The SASL password to use.</param>
        /// <returns>An instance which implements the IBucket interface.</returns>
        /// <remarks>Use Cluster.CloseBucket(bucket) to release resources associated with a Bucket.</remarks>
        public IBucket OpenBucket(string bucketName, string password)
        {
            return _clusterManager.CreateBucket(bucketName, password);
        }

        /// <summary>
        /// Creates a connection to a non-SASL Couchbase bucket.
        /// </summary>
        /// <param name="bucketName">The Couchbase Bucket to connect to.</param>
        /// <returns>An instance which implements the IBucket interface.</returns>
        /// <remarks>Use Cluster.CloseBucket(bucket) to release resources associated with a Bucket.</remarks>
        public IBucket OpenBucket(string bucketName)
        {
            return _clusterManager.CreateBucket(bucketName);
        }

        /// <summary>
        /// Returns an object representing cluster status information.
        /// </summary>
        public IClusterInfo Info
        {
            get { throw new NotImplementedException(); }
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
            _clusterManager.Dispose();
        }

        /// <summary>
        /// Closes and releases all resources associated with a Couchbase bucket.
        /// </summary>
        /// <param name="bucket">The Bucket to close.</param>
        public void CloseBucket(IBucket bucket)
        {
            _clusterManager.DestroyBucket(bucket);
        }
    }
}
