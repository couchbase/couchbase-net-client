using System;
using Couchbase.Management.Buckets;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates a BucketBase class based on <seealso cref="BucketType"/>.
    /// </summary>
    internal class BucketFactory : IBucketFactory
    {
        private readonly ClusterContext _clusterContext;
        private readonly ILogger<CouchbaseBucket> _couchbaseLogger;
        private readonly ILogger<MemcachedBucket> _memcachedLogger;

        public BucketFactory(ClusterContext clusterContext, ILogger<CouchbaseBucket> couchbaseLogger, ILogger<MemcachedBucket> memcachedLogger)
        {
            _clusterContext = clusterContext ?? throw new ArgumentNullException(nameof(clusterContext));
            _couchbaseLogger = couchbaseLogger ?? throw new ArgumentNullException(nameof(couchbaseLogger));
            _memcachedLogger = memcachedLogger ?? throw new ArgumentNullException(nameof(memcachedLogger));
        }

        /// <inheritdoc />
        public BucketBase Create(string name, BucketType bucketType) =>
            bucketType switch
            {
                BucketType.Couchbase => new CouchbaseBucket(name, _clusterContext, _couchbaseLogger),
                BucketType.Ephemeral => new CouchbaseBucket(name, _clusterContext, _couchbaseLogger),
                BucketType.Memcached => new MemcachedBucket(name, _clusterContext, _memcachedLogger),
                _ => throw new ArgumentOutOfRangeException(nameof(bucketType), bucketType, null)
            };
    }
}
