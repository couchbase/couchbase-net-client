using System;
using Couchbase.Core.Logging;
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
        private readonly IKetamaKeyMapperFactory _ketamaKeyMapperFactory;
        private readonly ILogger<CouchbaseBucket> _couchbaseLogger;
        private readonly ILogger<MemcachedBucket> _memcachedLogger;
        private readonly IRedactor _redactor;

        public BucketFactory(
            ClusterContext clusterContext,
            IScopeFactory scopeFactory,
            IRetryOrchestrator retryOrchestrator,
            IVBucketKeyMapperFactory vBucketKeyMapperFactory,
            IKetamaKeyMapperFactory ketamaKeyMapperFactory,
            ILogger<CouchbaseBucket> couchbaseLogger,
            ILogger<MemcachedBucket> memcachedLogger,
            IRedactor redactor
            )
        {
            _clusterContext = clusterContext ?? throw new ArgumentNullException(nameof(clusterContext));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _retryOrchestrator = retryOrchestrator ?? throw new ArgumentNullException(nameof(retryOrchestrator));
            _vBucketKeyMapperFactory = vBucketKeyMapperFactory ?? throw new ArgumentNullException(nameof(vBucketKeyMapperFactory));
            _ketamaKeyMapperFactory = ketamaKeyMapperFactory ?? throw new ArgumentNullException(nameof(ketamaKeyMapperFactory));
            _couchbaseLogger = couchbaseLogger ?? throw new ArgumentNullException(nameof(couchbaseLogger));
            _memcachedLogger = memcachedLogger ?? throw new ArgumentNullException(nameof(memcachedLogger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(IRedactor));
        }

        /// <inheritdoc />
        public BucketBase Create(string name, BucketType bucketType) =>
            bucketType switch
            {
                BucketType.Couchbase =>
                    new CouchbaseBucket(name, _clusterContext, _scopeFactory, _retryOrchestrator, _vBucketKeyMapperFactory, _couchbaseLogger, _redactor),
                BucketType.Ephemeral =>
                    new CouchbaseBucket(name, _clusterContext, _scopeFactory, _retryOrchestrator, _vBucketKeyMapperFactory, _couchbaseLogger, _redactor),
                BucketType.Memcached =>
                    new MemcachedBucket(name, _clusterContext, _scopeFactory, _retryOrchestrator, _ketamaKeyMapperFactory, _memcachedLogger, _redactor),
                _ => throw new ArgumentOutOfRangeException(nameof(bucketType), bucketType, null)
            };
    }
}
