using System;
using Couchbase.Configuration.Client;
using Couchbase.Core;

namespace Couchbase
{
    public class Cluster : ICluster
    {
        private readonly IClusterManager _clusterManager;
        private readonly ClientConfiguration _config;

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
    }
}
