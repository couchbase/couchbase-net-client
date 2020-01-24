using System;

namespace Couchbase.Core.Exceptions
{
    public class CollectionNotFoundException : CouchbaseException
    {
        public CollectionNotFoundException() { }

        public CollectionNotFoundException(string message) : base(message) { }

        public CollectionNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }
}
