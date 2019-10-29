using System;

namespace Couchbase.Core.Exceptions
{
    public class RequestCanceledException : CouchbaseException
    {
        public RequestCanceledException() { }

        public RequestCanceledException(string message) : base(message) { }

        public RequestCanceledException(string message, Exception innerException) : base(message, innerException) { }
    }
}
