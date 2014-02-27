using System;
using System.Runtime.Serialization;

namespace Couchbase.Core
{
    public class BucketNotFoundException : ArgumentException
    {
        public BucketNotFoundException()
        {
        }

        public BucketNotFoundException(string message) : base(message)
        {
        }

        public BucketNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public BucketNotFoundException(string message, string paramName, Exception innerException) : base(message, paramName, innerException)
        {
        }

        public BucketNotFoundException(string message, string paramName) : base(message, paramName)
        {
        }

        protected BucketNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
