using System;

namespace Couchbase.Core.Exceptions
{
    public class BucketNotFoundException : CouchbaseException
    {
        public BucketNotFoundException() { }

        public BucketNotFoundException(string message) : base(message) { }

        public BucketNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }
}
