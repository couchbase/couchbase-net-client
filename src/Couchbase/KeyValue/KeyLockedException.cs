using System;
using Couchbase.Core.Retry;

namespace Couchbase.KeyValue
{
    public class KeyLockedException: CouchbaseException, IRetryable
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
