using Couchbase.Core.Retry;

namespace Couchbase.Core.Exceptions.KeyValue
{
    public class SendQueueFullException : CouchbaseException, IRetryable
    {
        public SendQueueFullException() : base("The operation send queue is full.")
        {
        }
    }
}
