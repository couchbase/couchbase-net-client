using System;
using System.Net;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;

namespace Couchbase
{
    public class Cluster : ICluster
    {
        private readonly IClusterManager _clusterManager;
        private readonly ClientConfiguration _config;

        internal Cluster(ClientConfiguration config, Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory)
        {
            _config = config;
            _clusterManager = new ClusterManager(_config, ioStrategyFactory, connectionPoolFactory);
        }

        internal Cluster(ClientConfiguration config, Func<IConnectionPool, IOStrategy> ioStrategyFactory)
        {
            _config = config;
            _clusterManager = new ClusterManager(_config, ioStrategyFactory);
        }

        public Cluster(ClientConfiguration config)
        {
            _config = config;
            _clusterManager = new ClusterManager(_config);
        }

        public Cluster() 
            : this(new ClientConfiguration())
        {
        }

        void Initialize()
        {
            
        }

        public IBucket OpenBucket(string bucketName, string password)
        {
            return _clusterManager.CreateBucket(bucketName, password);
        }
        
        public IBucket OpenBucket(string bucketName)
        {
            return _clusterManager.CreateBucket(bucketName);
        }

        public IClusterInfo Info
        {
            get { throw new NotImplementedException(); }
        }

        public void Dispose()
        {
            //There is a bug here somewhere - note that when called this should close and cleanup _everything_
            //however, if you do not explicitly call Cluster.CloseBucket(bucket) in certain cases the HttpStreamingProvider
            //listener thread will hang indefinitly if Cluster.Dispose() is called. This is a definite bug that needs to be
            //resolved before developer preview.
            _clusterManager.Dispose();
        }

        //TODO: not sure what to do here if bucket doesn't exist...the current impl is to throw a BucketNotFoundException. I am 
        //not sure if this is the correct behavior, since it's causing me some grief with my unit tests and I can assume that 
        //users will run into the same grief
        public void CloseBucket(IBucket bucket)
        {
            _clusterManager.DestroyBucket(bucket);
        }
    }
}
