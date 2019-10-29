using System;

namespace Couchbase.Core.Retry.Query
{
    public static class RequestExtensions
    {
        public static void IncrementAttempts(this IRequest request, RetryReason reason)
        {
            request.Attempts++;
            request.RetryReasons.Add(reason);
        }
    }
}
