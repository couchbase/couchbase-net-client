using Couchbase.Core.IO.Operations;


namespace Couchbase.Core.Retry
{
    public interface IRetryStrategy
    {
        RetryAction RetryAfter(IOperation operation, RetryReason reason);
    }
}
