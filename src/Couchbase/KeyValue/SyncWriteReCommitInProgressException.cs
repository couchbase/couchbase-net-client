using System;
using Couchbase.Core.Retry;

namespace Couchbase.KeyValue
{
    public class SyncWriteReCommitInProgressException : CouchbaseException, IRetryable
    {
        public SyncWriteReCommitInProgressException()
        {
        }

        public SyncWriteReCommitInProgressException(string message)
            : base(message)
        {
        }

        public SyncWriteReCommitInProgressException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
