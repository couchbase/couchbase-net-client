namespace Couchbase.Core.Retry
{
    public interface IRetryStrategy
    {
        RetryAction RetryAfter(IRequest request, RetryReason reason);
    }
}
