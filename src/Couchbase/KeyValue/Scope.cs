using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <remarks>Volatile</remarks>
    internal class Scope : IScope
    {
        public const string DefaultScopeName = "_default";

        private readonly BucketBase _bucket;
        private readonly ILogger<Scope> _logger;
        private readonly ConcurrentDictionary<string, ICollection> _collections;

        public Scope(string name, string id, IEnumerable<ICollection> collections, BucketBase bucket, ILogger<Scope> logger)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            _collections = new ConcurrentDictionary<string, ICollection>(collections.ToDictionary(x => x.Name, v => v));
            _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Id { get; }

        public string Name { get; }

        public ICollection this[string name]
        {
            get
            {
                _logger.LogDebug("Fetching collection {collectionName}.", name);

                if(_collections.TryGetValue(name, out ICollection collection))
                {
                    return collection;
                }

                //return the default bucket which will fail on first op invocation
                if (!(_bucket as IBootstrappable).IsBootstrapped)
                {
                    return _bucket.DefaultCollection();
                }

                throw new CollectionNotFoundException($"Cannot find collection {name}");
            }
        }

        /// <summary>
        /// Returns a given collection by name.
        /// </summary>
        /// <param name="collectionName"></param>
        /// <returns></returns>
        /// <remarks>Volatile</remarks>
        public ICollection Collection(string collectionName)
        {
            return this[collectionName];
        }
    }
}
