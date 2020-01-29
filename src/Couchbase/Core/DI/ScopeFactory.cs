using System;
using System.Collections.Generic;
using Couchbase.Core.Configuration.Server;
using Couchbase.KeyValue;
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
                var collections = new List<ICollection>();
                foreach (var collectionDef in scopeDef.collections)
                {
                    collections.Add(_collectionFactory.Create(bucket,
                        Convert.ToUInt32(collectionDef.uid, 16),
                        collectionDef.name,
                        scopeDef.name));
                }

                yield return new Scope(scopeDef.name, scopeDef.uid, collections, bucket, _scopeLogger);
            }
        }

        /// <inheritdoc />
        public IScope CreateDefaultScope(BucketBase bucket)
        {
            if (bucket == null)
            {
                throw new ArgumentNullException(nameof(bucket));
            }

            var collections = new List<ICollection>
            {
                _collectionFactory.Create(bucket, null,
                    CouchbaseCollection.DefaultCollectionName, Scope.DefaultScopeName)
            };

            return new Scope(Scope.DefaultScopeName, "0", collections, bucket, _scopeLogger);
        }
    }
}
