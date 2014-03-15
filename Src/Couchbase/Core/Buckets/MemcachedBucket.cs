 using System;
 using System.Threading;
 using Common.Logging;
 using Couchbase.Configuration;
 using Couchbase.Configuration.Server.Providers;
 using Couchbase.IO.Operations;

namespace Couchbase.Core.Buckets
{
    public class MemcachedBucket : IBucket, IConfigListener
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IClusterManager _clusterManager;
        private IConfigInfo _configInfo;
        private IKeyMapper _keyMapper;
        private volatile bool _disposed;

         internal MemcachedBucket(IClusterManager clusterManager, string bucketName)
        {
            _clusterManager = clusterManager;
            Name = bucketName;
        }

        public string Name { get; set; }

        public IOperationResult<T> Insert<T>(string key, T value)
        {
            throw new NotImplementedException();
        }

        public IOperationResult<T> Get<T>(string key)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }


        void IConfigListener.NotifyConfigChanged(IConfigInfo configInfo)
        {
            Interlocked.Exchange(ref _configInfo, configInfo);
        }
    }
}
