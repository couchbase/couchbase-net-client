using System;
using Couchbase.Core.Compatibility;

namespace Couchbase.Management.Collections
{
    public class CollectionSpec
    {
        /// <summary>
        /// The Collection name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The Scope name.
        /// </summary>
        public string ScopeName { get; }

        /// <summary>
        /// MaxExpiry is the time in seconds for the TTL for new documents in the collection. It will be infinite if not set.
        /// </summary>
        [InterfaceStability(Level.Volatile)]
        public TimeSpan? MaxExpiry { get; set; }

        public CollectionSpec(string scopeName, string name)
        {
            ScopeName = scopeName;
            Name = name;
        }
    }
}
