using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase
{
    public class Scope : IScope
    {
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
                if(_collections.TryGetValue(name, out ICollection collection))
                {
                    return collection;
                };
                throw new CollectionNotFoundException("Cannot find collection {name}");
            }
        }

        public ICollection Collection(string name)
        {
            return this[name];
        }
    }
}
