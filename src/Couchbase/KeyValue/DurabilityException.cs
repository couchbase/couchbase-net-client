using System;

namespace Couchbase.KeyValue
{
    public class DurabilityException : CouchbaseException
    {
        public DurabilityException()
        {
        }

        public DurabilityException(string message)
            : base(message)
        {
        }

        public DurabilityException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
