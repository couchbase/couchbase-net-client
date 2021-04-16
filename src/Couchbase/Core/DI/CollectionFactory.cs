using System;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
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
        private readonly IOperationConfigurator _operationConfigurator;
        private readonly ILogger<CouchbaseCollection> _logger;
        private readonly ILogger<GetResult> _getLogger;
        private readonly IRedactor _redactor;
        private readonly IRequestTracer _tracer;

        public CollectionFactory(IOperationConfigurator operationConfigurator, ILogger<CouchbaseCollection> logger,
            ILogger<GetResult> getLogger, IRedactor redactor, IRequestTracer tracer)
        {
            _operationConfigurator = operationConfigurator ?? throw new ArgumentNullException(nameof(operationConfigurator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _getLogger = getLogger ?? throw new ArgumentNullException(nameof(getLogger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _tracer = tracer;
        }

        /// <inheritdoc />
        public ICouchbaseCollection Create(BucketBase bucket, IScope scope, string name) =>
            new CouchbaseCollection(bucket, _operationConfigurator, _logger, _getLogger, _redactor, name, scope, _tracer);
    }
}
