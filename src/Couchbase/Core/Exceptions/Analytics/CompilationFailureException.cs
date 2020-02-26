using System;

namespace Couchbase.Core.Exceptions.Analytics
{
    public class CompilationFailureException : CouchbaseException
    {
        public CompilationFailureException()
        {
        }

        public CompilationFailureException(IErrorContext context) : base(context)
        {
        }

        public CompilationFailureException(string message) : base(message)
        {
        }

        public CompilationFailureException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
