using System;
using System.Collections.Generic;
using Couchbase.Core.Retry;

namespace Couchbase.Core.Exceptions
{
    public class UnambiguousTimeoutException : CouchbaseException
    {
        public UnambiguousTimeoutException() { }

        public UnambiguousTimeoutException(IErrorContext context)
        {
            Context = context;
        }

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
