using System;
using System.Collections.Generic;
using System.Threading;

#nullable enable

namespace Couchbase.Core.Retry
{
    public interface IRequest
    {
        uint Attempts { get; set; }
        bool Idempotent { get; }
        List<RetryReason> RetryReasons { get; set; }
        IRetryStrategy RetryStrategy { get; set; }
        TimeSpan Timeout { get; set; }
        CancellationToken Token { get; set; }

        /// <summary>
        /// Gets the context identifier for the analytics request. Useful for debugging.
        /// </summary>
        /// <returns>The unique request ID.</returns>.
        /// <remarks>
        /// This value changes for every request.
        /// </remarks>
        string? ClientContextId { get; set; }
        string? Statement { get; set; }
    }
}
