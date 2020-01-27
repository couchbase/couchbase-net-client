using System;

namespace Couchbase.Core.Exceptions
{
    public class UnsupportedException : CouchbaseException
    {
        public UnsupportedException(string message) : base(message) { }

        public UnsupportedException(string message, Exception innerException) : base(message, innerException) { }
    }
}
