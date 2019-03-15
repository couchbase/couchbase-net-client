using System;

namespace Couchbase
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
