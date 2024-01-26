using System;

namespace Couchbase.Core.Exceptions
{
    public class ConnectException : CouchbaseException
    {
        public ConnectException(string message) : base(message)
        {
        }

        public ConnectException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
