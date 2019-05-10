using System;

namespace Couchbase.Core.Configuration.Server
{
    public class ContextStoppedException : Exception
    {
        public ContextStoppedException()
        {
        }

        public ContextStoppedException(string message) : base(message)
        {
        }

        public ContextStoppedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
