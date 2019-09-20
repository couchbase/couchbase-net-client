using System;

namespace Couchbase.Services.KeyValue
{
    /// <summary>
    /// A generic error rasied where the value for a key cannot be found.
    /// </summary>
    public class KeyNotFoundException : CouchbaseException
    {
        public KeyNotFoundException()
        {
        }

        public KeyNotFoundException(string message)
            : base(message)
        {
        }

        public KeyNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public Exception Reason => InnerException;
    }
}
