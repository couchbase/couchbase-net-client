using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core.Retry;
using Newtonsoft.Json;

namespace Couchbase.Core.Exceptions
{
    public class AmbiguousTimeoutException : CouchbaseException
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
