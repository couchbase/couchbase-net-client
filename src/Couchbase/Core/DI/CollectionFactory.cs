using System;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Default implementation of <see cref="ICollectionFactory"/>.
    /// </summary>
    internal class CollectionFactory : ICollectionFactory
    {
        private readonly ITypeTranscoder _transcoder;
        private readonly ILogger<CouchbaseCollection> _logger;

        public CollectionFactory(ITypeTranscoder transcoder, ILogger<CouchbaseCollection> logger)
        {
            _transcoder = transcoder ?? throw new ArgumentNullException(nameof(transcoder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public ICollection Create(BucketBase bucket, uint? cid, string name, string scopeName) =>
            new CouchbaseCollection(bucket, _transcoder, _logger, cid, name, scopeName);
    }
}
