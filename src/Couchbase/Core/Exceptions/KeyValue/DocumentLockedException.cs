using Couchbase.Core.Retry;

namespace Couchbase.Core.Exceptions.KeyValue
{
    public class DocumentLockedException : CouchbaseException, IRetryable
    {
    }
}
