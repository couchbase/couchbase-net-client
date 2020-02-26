using System;

namespace Couchbase.Core.Exceptions
{
    public class InternalServerFailureException : CouchbaseException
    {
        public InternalServerFailureException()
        {
        }

        public InternalServerFailureException(IErrorContext context)
        {
            Context = context;
        }

        public InternalServerFailureException(string message) : base(message)
        {
        }

        public InternalServerFailureException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
