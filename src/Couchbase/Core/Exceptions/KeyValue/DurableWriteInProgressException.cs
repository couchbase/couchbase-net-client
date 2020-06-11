using Couchbase.Core.Retry;

namespace Couchbase.Core.Exceptions.KeyValue
{
    public class DurableWriteInProgressException : CouchbaseException, IRetryable
    {
    }
}
