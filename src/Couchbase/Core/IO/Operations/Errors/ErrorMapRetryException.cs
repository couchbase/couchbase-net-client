using System;
using Couchbase.Core.Retry;

namespace Couchbase.Core.IO.Operations.Errors
{
    public class ErrorMapRetryException : CouchbaseException, IRetryable
    {
        public ErrorMapRetryException()
        {
        }

        public ErrorMapRetryException(string message) : base(message)
        {
        }

        public ErrorMapRetryException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
