using Couchbase.Core.IO.Operations.Legacy;

namespace Couchbase.Core.Retry
{
    public class BestEffortRetryStrategy : IRetryStrategy
    {
        private readonly IBackoffCalculator _backoffCalculator;

        public BestEffortRetryStrategy() :
            this(ExponentialBackoff.Create(10, 1, 500))
        {
        }

        public BestEffortRetryStrategy(IBackoffCalculator calculator)
        {
            _backoffCalculator = calculator;
        }

        public RetryAction RetryAfter(IOperation operation, RetryReason reason)
        {
            if (operation.Idempotent || reason.AllowsNonIdempotentRetries())
            {
                var backoffDuration = _backoffCalculator.CalculateBackoff(operation);
                return RetryAction.WithDuration(backoffDuration);
            }

            return RetryAction.WithDuration(null);
        }
    }
}
