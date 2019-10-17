using System;

namespace Couchbase
{
    public class ServiceMissingException : CouchbaseException
    {
        public ServiceMissingException()
        {
        }

        public ServiceMissingException(string message) : base(message)
        {
        }

        public ServiceMissingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
