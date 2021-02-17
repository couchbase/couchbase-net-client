using System;
using Couchbase.Core.Retry;

namespace Couchbase.Core.Exceptions
{
    public class CollectionNotFoundException : CouchbaseException, IRetryable
    {
        public CollectionNotFoundException() { }

        public CollectionNotFoundException(string message) : base(message) { }

        public CollectionNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }
}
