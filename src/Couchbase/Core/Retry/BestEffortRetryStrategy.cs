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

        public RetryAction RetryAfter(IRequest request, RetryReason reason)
        {
            if (request.Idempotent || reason.AllowsNonIdempotentRetries())
            {
                var backoffDuration = _backoffCalculator.CalculateBackoff(request);
                return RetryAction.WithDuration(backoffDuration);
            }

            return RetryAction.WithDuration(null);
        }
    }
}
