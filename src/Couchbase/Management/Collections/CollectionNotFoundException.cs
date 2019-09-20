using System;

namespace Couchbase.Management.Collections
{
    public class CollectionNotFoundException : Exception
    {
        public CollectionNotFoundException(string scopeName, string collectionName)
            : base($"Collection with name {collectionName} not found in scope {scopeName}")
        { }
    }
}
