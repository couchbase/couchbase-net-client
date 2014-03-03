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

        public IBucket OpenBucket(string bucketName, string passWord, string userName)
        {
            throw new NotImplementedException();
        }
        
        public IBucket OpenBucket(string bucketName)
        {
            var bucket = _clusterManager.CreateBucket(bucketName);
            return bucket;
        }

        public IClusterInfo Info
        {
            get { throw new NotImplementedException(); }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }


        public void CloseBucket(IBucket bucket)
        {
            _clusterManager.DestroyBucket(bucket);
        }
    }
}
