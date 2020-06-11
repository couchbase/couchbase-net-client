using System;
using Couchbase.Core.Retry;

namespace Couchbase.Core.Exceptions
{
    public class TemporaryFailureException : CouchbaseException, IRetryable
    {
        public TemporaryFailureException() { }

        public TemporaryFailureException(string message) : base(message) { }

        public TemporaryFailureException(string message, Exception innerException) : base(message, innerException) { }

        public TemporaryFailureException(IErrorContext context) : base(context)
        {
        }
    }
}
