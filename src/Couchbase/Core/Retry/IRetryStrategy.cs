using System;
using System.Collections.Generic;
using System.Text;

namespace RetryExample
{
    public interface IRetryStrategy
    {
        RetryAction RetryAfter(IOperation operation, RetryReason reason);
    }
}
