using System;

namespace Couchbase.Services.KeyValue
{
    public class KeyLockedException: CouchbaseException
    {
        public KeyLockedException()
        {
        }

        public KeyLockedException(string message)
            : base(message)
        {
        }

        public KeyLockedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
