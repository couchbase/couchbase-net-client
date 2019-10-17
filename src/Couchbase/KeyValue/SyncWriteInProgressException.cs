using System;
using Couchbase.Core.Retry;

namespace Couchbase.KeyValue
{
    public class SyncWriteInProgressException : CouchbaseException, IRetryable
    {
        public SyncWriteInProgressException()
        {
        }

        public SyncWriteInProgressException(string message)
            : base(message)
        {
        }

        public SyncWriteInProgressException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
