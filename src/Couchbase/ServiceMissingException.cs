using System;
using System.Runtime.Serialization;

namespace Couchbase
{
    public class ServiceMissingException : Exception
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
