using System;
using System.Collections.Generic;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
using Couchbase.KeyValue;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Default implementation of <see cref="IScopeFactory"/>.
    /// </summary>
    internal class ScopeFactory : IScopeFactory
    {
        private readonly ILogger<Scope> _scopeLogger;
        private readonly ICollectionFactory _collectionFactory;
        private readonly IRequestTracer _tracer;
        private readonly IOperationConfigurator _configurator;

        public ScopeFactory(ILogger<Scope> scopeLogger, ICollectionFactory collectionFactory, IRequestTracer tracer, IOperationConfigurator configurator)
        {
            _scopeLogger = scopeLogger;
            _collectionFactory = collectionFactory ?? throw new ArgumentNullException(nameof(collectionFactory));
            _tracer = tracer;
            _configurator = configurator;
        }

        /// <inheritdoc />
        public IEnumerable<IScope> CreateScopes(BucketBase bucket, Manifest manifest)
        {
            if (bucket == null)
            {
                throw new ArgumentNullException(nameof(bucket));
            }
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            foreach (var scopeDef in manifest.scopes)
            {
                yield return new Scope(scopeDef, _collectionFactory, bucket, _scopeLogger, _tracer, _configurator);
            }
        }

        /// <inheritdoc />
        public IScope CreateDefaultScope(BucketBase bucket)
        {
            if (bucket == null)
            {
                throw new ArgumentNullException(nameof(bucket));
            }

            return new Scope(null, _collectionFactory, bucket, _scopeLogger, _tracer, _configurator);
        }

        /// <inheritdoc />
        public IScope CreateScope(string name, string scopeIdentifier, BucketBase bucket)
        {
            if (name == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(name));
            }
            if (scopeIdentifier == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(scopeIdentifier));
            }
            if (bucket == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(bucket));
            }

            return new Scope(name, scopeIdentifier, bucket,_collectionFactory, _scopeLogger, _tracer, _configurator);
        }
    }
}
