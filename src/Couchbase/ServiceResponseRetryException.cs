using System;
using Couchbase.Core.Retry;

namespace Couchbase
{
    public class ServiceResponseRetryException : CouchbaseException, IRetryable
    {
        public ServiceResponseRetryException()
        {
        }

        public ServiceResponseRetryException(string message)
            : base(message)
        {
        }

        public ServiceResponseRetryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
