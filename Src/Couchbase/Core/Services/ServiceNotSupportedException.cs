using System;
using System.Runtime.Serialization;

namespace Couchbase.Core.Services
{
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