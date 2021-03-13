using System;
using System.Collections.Generic;
using Couchbase.Core.Retry;

namespace Couchbase.Core.Exceptions
{
    /// <summary>
    /// A <see cref="TimeoutException"/> where we are sure there was no side effect on the server.
    /// For example an idempotent operation timeout.
    /// </summary>
    public class UnambiguousTimeoutException : TimeoutException
    {
        public UnambiguousTimeoutException() { }

        public UnambiguousTimeoutException(string message) : base(message) { }

        public UnambiguousTimeoutException(string message, Exception innerException) : base(message, innerException) { }

        public List<RetryReason> RetryReasons { get; } = new List<RetryReason>();

        internal static void ThrowWithRetryReasons(IRequest request, Exception innerException = null)
        {
            var exception = new UnambiguousTimeoutException("The request has timed out.", innerException);
            foreach (var retryReason in request.RetryReasons)
            {
                exception.RetryReasons.Add(retryReason);
            }

            throw exception;
        }
    }
}
