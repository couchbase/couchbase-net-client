using System;

namespace Couchbase.Core.Exceptions
{
    public class ParsingFailureException : CouchbaseException
    {
        public ParsingFailureException() { }

        public ParsingFailureException(IErrorContext context)
        {
            Context = context;
        }

        public ParsingFailureException(string message) : base(message)
        {
        }

        public ParsingFailureException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
