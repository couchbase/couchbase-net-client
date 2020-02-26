using System;

namespace Couchbase.Core.Exceptions
{
    public class IndexNotFoundException : CouchbaseException
    {
        public IndexNotFoundException(){}

        public IndexNotFoundException(IErrorContext context)
        {
            Context = context;
        }

        public IndexNotFoundException(string message) : base(message)
        {
        }

        public IndexNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
