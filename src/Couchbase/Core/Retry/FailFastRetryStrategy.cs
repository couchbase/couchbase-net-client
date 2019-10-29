namespace Couchbase.Core.Retry
{
    /// <summary>
    /// Used internally to skip retries when specified. 
    /// </summary>
    internal class FailFastRetryStrategy : IRetryStrategy
    {
        public RetryAction RetryAfter(IRequest request, RetryReason reason)
        {
            return RetryAction.WithDuration(null);
        }
    }
}
