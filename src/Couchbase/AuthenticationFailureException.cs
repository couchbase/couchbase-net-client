using System;
using Couchbase.Core;

namespace Couchbase
{
    public class AuthenticationFailureException : CouchbaseException
    {
        public AuthenticationFailureException()
        {
        }

        public AuthenticationFailureException(string message)
            : base(message)
        {
        }

        public AuthenticationFailureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public AuthenticationFailureException(IErrorContext context) : base(context)
        {
        }
    }
}
