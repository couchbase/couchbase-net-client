using System;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
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
        private readonly ILogger<GetResult> _getLogger;
        private readonly IRedactor _redactor;

        public CollectionFactory(ITypeTranscoder transcoder, ILogger<CouchbaseCollection> logger,
            ILogger<GetResult> getLogger, IRedactor redactor)
        {
            _transcoder = transcoder ?? throw new ArgumentNullException(nameof(transcoder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _getLogger = getLogger ?? throw new ArgumentNullException(nameof(getLogger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        /// <inheritdoc />
        public ICouchbaseCollection Create(BucketBase bucket, IScope scope, uint? cid, string name) =>
            new CouchbaseCollection(bucket, _transcoder, _logger, _getLogger, _redactor, cid, name, scope);
    }
}
