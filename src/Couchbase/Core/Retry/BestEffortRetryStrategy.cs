using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core.IO.Operations.Legacy;

namespace RetryExample
{  
    public class BestEffortRetryStrategy : IRetryStrategy
    {
        public RetryAction RetryAfter(IOperation operation, RetryReason reason)
        {
            if (operation.Idempotent || reason.AllowsNonIdempotentRetries())
            {
                return RetryAction.WithDuration(CalculateDuration(operation.Attempts));
            }

            return RetryAction.WithDuration(null);
        }

        public TimeSpan CalculateDuration(uint retryAttempts)
        {
            switch (retryAttempts)
            {
                case 0:
                    return TimeSpan.FromMilliseconds(1);
                case 1:
                    return TimeSpan.FromMilliseconds(10);
                case 2:
                    return TimeSpan.FromMilliseconds(50);
                case 3:
                    return TimeSpan.FromMilliseconds(100);
                case 4:
                    return TimeSpan.FromMilliseconds(500);
                default:
                    return TimeSpan.FromMilliseconds(1000);
            }
        }
    }
}
