using System;

namespace Couchbase
{
    public class NetworkErrorException : CouchbaseException
    {
        public NetworkErrorException()
        {
        }

        public NetworkErrorException(string message)
            : base(message)
        {
        }

        public NetworkErrorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
