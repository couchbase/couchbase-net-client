using Couchbase.Core.Retry;

namespace Couchbase.Core.Exceptions.KeyValue
{
    public class DurableWriteReCommitInProgressException : CouchbaseException, IRetryable
    {
    }
}
