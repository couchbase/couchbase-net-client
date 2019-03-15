using System;

namespace Couchbase
{
    public class InternalErrorException : CouchbaseException
    {
        public InternalErrorException()
        {
        }

        public InternalErrorException(string message)
            : base(message)
        {
        }

        public InternalErrorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
