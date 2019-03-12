using System;

namespace Couchbase
{
    public class BucketMissingException : Exception
    {
        public BucketMissingException()
        {
        }

        public BucketMissingException(string message) : base(message)
        {
        }

        public BucketMissingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
