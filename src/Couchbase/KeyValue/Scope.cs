using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Couchbase.KeyValue
{
    public class Scope : IScope
    {
        private static readonly ILogger Log =  LogManager.CreateLogger<Scope>();
        private readonly IBucket _bucket;
        private readonly ConcurrentDictionary<string, ICollection> _collections;
        internal Scope(string name,  string id, IEnumerable<ICollection> collections, IBucket bucket)
        {
            Name = name;
            Id = id;
            _collections = new ConcurrentDictionary<string, ICollection>(collections.ToDictionary(x => x.Name, v => v));
            _bucket = bucket;
        }

        public string Id { get; }

        public string Name { get; }

        public ICollection this[string name]
        {
            get
            {
                Log.LogDebug($"Fetching collection {0}", name);

                if(_collections.TryGetValue(name, out ICollection collection))
                {
                    return collection;
                };
                throw new CollectionOutdatedException($"Cannot find collection {name}");
            }
        }

        public ICollection Collection(string collectionName)
        {
            return this[collectionName];
        }
    }
}
