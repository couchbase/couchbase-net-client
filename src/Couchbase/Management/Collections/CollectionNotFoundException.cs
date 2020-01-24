using System;

namespace Couchbase.Management.Collections
{
    public class CollectionNotFoundException : CouchbaseException
    {
        public CollectionNotFoundException(string scopeName, string collectionName)
            : base($"Collection with name {collectionName} not found in scope {scopeName}")
        { }
    }
}
