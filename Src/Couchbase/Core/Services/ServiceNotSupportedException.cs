using System;
using System.Runtime.Serialization;

namespace Couchbase.Core.Services
{
    /// <summary>
    /// Thrown when an application makes a request (query, view, data) on a cluster for which the service has not been configured.
    /// </summary>
    public class ServiceNotSupportedException : NotSupportedException
    {
        public ServiceNotSupportedException()
        {
        }

        public ServiceNotSupportedException(string message)
            : base(message)
        {
        }

        public ServiceNotSupportedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected ServiceNotSupportedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}