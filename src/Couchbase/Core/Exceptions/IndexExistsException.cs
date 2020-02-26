using System;

namespace Couchbase.Core.Exceptions
{
    public class IndexExistsException : CouchbaseException
    {
        public IndexExistsException(){}

        public IndexExistsException(IErrorContext context)
        {
            Context = context;
        }

        public IndexExistsException(string message) : base(message)
        {
        }

        public IndexExistsException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
