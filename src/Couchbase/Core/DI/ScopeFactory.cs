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

        public ScopeFactory(ILogger<Scope> scopeLogger, ICollectionFactory collectionFactory)
        {
            _scopeLogger = scopeLogger;
            _collectionFactory = collectionFactory ?? throw new ArgumentNullException(nameof(collectionFactory));
        }

        /// <inheritdoc />
        public IScope CreateScope(string name, BucketBase bucket)
        {
            return new Scope(name, bucket,_collectionFactory, _scopeLogger);
        }
    }
}
