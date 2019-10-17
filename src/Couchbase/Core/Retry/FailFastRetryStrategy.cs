using Couchbase.Core.IO.Operations.Legacy;

namespace Couchbase.Core.Retry
{
    public class FailFastRetryStrategy : IRetryStrategy
    {
        public RetryAction RetryAfter(IOperation ret, RetryReason reason)
        {
            return RetryAction.WithDuration(null);
        }
    }
}
