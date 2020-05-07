using System;

namespace Couchbase.Core.Exceptions
{
    public class InternalServerFailureException : CouchbaseException
    {
        public InternalServerFailureException()
        {
        }

        public InternalServerFailureException(IErrorContext context) : base(context.Message)
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
