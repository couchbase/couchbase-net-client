using System;
using System.Collections.Generic;
using System.Net;
using Couchbase.Core.Sharding;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Default implementation of <see cref="IVBucketFactory"/>.
    /// </summary>
    internal class VBucketFactory : IVBucketFactory
    {
        private readonly ILogger<VBucket> _logger;

        public VBucketFactory(ILogger<VBucket> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public IVBucket Create(ICollection<IPEndPoint> endPoints, short index, short primary,
            short[] replicas, ulong rev, VBucketServerMap vBucketServerMap, string bucketName) =>
            new VBucket(endPoints, index, primary, replicas, rev, vBucketServerMap, bucketName, _logger);
    }
}
