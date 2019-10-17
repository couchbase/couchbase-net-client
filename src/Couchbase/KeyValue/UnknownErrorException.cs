using System;

namespace Couchbase.KeyValue
{
    public class UnknownErrorException : CouchbaseException
    {
        public UnknownErrorException()
        {
        }

        public UnknownErrorException(string message)
            : base(message)
        {
        }

        public UnknownErrorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
