using System;
using System.Collections.Generic;
using System.Text;

namespace RetryExample
{
    public class FailFastRetryStrategy : IRetryStrategy
    {
        public RetryAction RetryAfter(IOperation operation, RetryReason reason)
        {
            return RetryAction.WithDuration(null);
        }
    }
}
