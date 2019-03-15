using System;

namespace Couchbase
{
    /// <summary>
    /// A generic error raised when the Bucket for a resource cannot be located.
    /// </summary>
    public class BucketMissingException : CouchbaseException
    {
        public BucketMissingException()
        {
        }

        public BucketMissingException(string message)
            : base(message)
        {
        }

        public BucketMissingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
