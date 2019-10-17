using System;
using Couchbase.Core.Retry;

namespace Couchbase.KeyValue
{
    public class NotMyVBucketException : CouchbaseException, IRetryable
    {
        public NotMyVBucketException()
        {
        }

        public NotMyVBucketException(string message)
            : base(message)
        {
        }

        public NotMyVBucketException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
