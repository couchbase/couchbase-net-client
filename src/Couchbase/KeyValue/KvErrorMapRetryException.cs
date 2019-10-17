using System;

namespace Couchbase.KeyValue
{
    public class KvErrorMapRetryException : CouchbaseException
    {
        public KvErrorMapRetryException()
        {
        }

        public KvErrorMapRetryException(string message) : base(message)
        {
        }

        public KvErrorMapRetryException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
