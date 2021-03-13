using System;
using System.Collections.Generic;
using Couchbase.Core.Retry;

namespace Couchbase.Core.Exceptions
{
    /// <summary>
    /// A <see cref="TimeoutException"/> where there may be a side effect on the server. A timeout
    /// happened while performing an non-idempotent operation.
    /// </summary>
    public class AmbiguousTimeoutException : TimeoutException
    {
        public AmbiguousTimeoutException() { }

        public AmbiguousTimeoutException(IErrorContext context)
        {
            Context = context;
        }

        public AmbiguousTimeoutException(string message) : base(message) { }

        public AmbiguousTimeoutException(string message, Exception innerException) : base(message, innerException) { }

        public List<RetryReason> RetryReasons { get; } = new List<RetryReason>();

        internal static void ThrowWithRetryReasons(IRequest request, Exception innerException = null)
        {
            var exception = new AmbiguousTimeoutException("The request has timed out.", innerException);
            foreach (var retryReason in request.RetryReasons)
            {
                exception.RetryReasons.Add(retryReason);
            }

            throw exception;
        }
    }
}
