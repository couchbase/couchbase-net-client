using System;

namespace Couchbase.Core.Exceptions
{
    public class InvalidIndexException : CouchbaseException
    {
        public InvalidIndexException() { }

        public InvalidIndexException(string message) : base(message) { }

        public InvalidIndexException(string message, Exception innerException) : base(message, innerException) { }
    }
}
