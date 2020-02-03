using System;
using Couchbase.Core.Retry;
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
        private readonly IScopeFactory _scopeFactory;
        private readonly IRetryOrchestrator _retryOrchestrator;
        private readonly IVBucketKeyMapperFactory _vBucketKeyMapperFactory;
        private readonly ILogger<CouchbaseBucket> _couchbaseLogger;
        private readonly ILogger<MemcachedBucket> _memcachedLogger;

        public BucketFactory(
            ClusterContext clusterContext,
            IScopeFactory scopeFactory,
            IRetryOrchestrator retryOrchestrator,
            IVBucketKeyMapperFactory vBucketKeyMapperFactory,
            ILogger<CouchbaseBucket> couchbaseLogger,
            ILogger<MemcachedBucket> memcachedLogger)
        {
            _clusterContext = clusterContext ?? throw new ArgumentNullException(nameof(clusterContext));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _retryOrchestrator = retryOrchestrator ?? throw new ArgumentNullException(nameof(retryOrchestrator));
            _vBucketKeyMapperFactory = vBucketKeyMapperFactory ?? throw new ArgumentNullException(nameof(vBucketKeyMapperFactory));
            _couchbaseLogger = couchbaseLogger ?? throw new ArgumentNullException(nameof(couchbaseLogger));
            _memcachedLogger = memcachedLogger ?? throw new ArgumentNullException(nameof(memcachedLogger));
        }

        /// <inheritdoc />
        public BucketBase Create(string name, BucketType bucketType) =>
            bucketType switch
            {
                BucketType.Couchbase =>
                    new CouchbaseBucket(name, _clusterContext, _scopeFactory, _retryOrchestrator, _vBucketKeyMapperFactory, _couchbaseLogger),
                BucketType.Ephemeral =>
                    new CouchbaseBucket(name, _clusterContext, _scopeFactory, _retryOrchestrator, _vBucketKeyMapperFactory, _couchbaseLogger),
                BucketType.Memcached =>
                    new MemcachedBucket(name, _clusterContext, _scopeFactory, _retryOrchestrator, _memcachedLogger),
                _ => throw new ArgumentOutOfRangeException(nameof(bucketType), bucketType, null)
            };
    }
}
