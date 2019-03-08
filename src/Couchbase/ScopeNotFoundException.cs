using System;

namespace Couchbase
{
    public class ScopeNotFoundException : Exception
    {
        public ScopeNotFoundException()
        {
        }

        public ScopeNotFoundException(string message) : base(message)
        {
        }

        public ScopeNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
