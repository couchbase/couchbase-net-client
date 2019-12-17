using System;

namespace Couchbase.Management.Collections
{
    public class CollectionExistsException : Exception
    {
        public CollectionExistsException(string scopeName, string collectionName)
            : base($"Collection with name {collectionName} already exists in scope {scopeName}")
        { }
    }
}
