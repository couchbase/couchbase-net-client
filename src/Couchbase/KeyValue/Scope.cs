using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <remarks>Volatile</remarks>
    public class Scope : IScope
    {
        private static readonly ILogger Log =  LogManager.CreateLogger<Scope>();
        private readonly IBucket _bucket;
        private readonly ConcurrentDictionary<string, ICollection> _collections;

        internal Scope(string name,  string id, IEnumerable<ICollection> collections, IBucket bucket)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            _collections = new ConcurrentDictionary<string, ICollection>(collections.ToDictionary(x => x.Name, v => v));
            _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
        }

        public string Id { get; }

        public string Name { get; }

        public ICollection this[string name]
        {
            get
            {
                Log.LogDebug($"Fetching collection {name}.");

                if(_collections.TryGetValue(name, out ICollection collection))
                {
                    return collection;
                }

                //return the default bucket which will fail on first op invocation
                if (((BucketBase) _bucket).BootstrapErrors)
                {
                    return _bucket.DefaultCollection();
                }
                throw new CollectionOutdatedException($"Cannot find collection {name}");
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
