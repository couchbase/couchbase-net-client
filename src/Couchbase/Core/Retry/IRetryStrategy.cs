using Couchbase.Core.IO.Operations.Legacy;

namespace Couchbase.Core.Retry
{
    public interface IRetryStrategy
    {
        RetryAction RetryAfter(IOperation operation, RetryReason reason);
    }
}
