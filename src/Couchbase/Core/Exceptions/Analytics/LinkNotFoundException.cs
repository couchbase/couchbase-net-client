using System;

namespace Couchbase.Core.Exceptions.Analytics
{
    public class LinkNotFoundException : CouchbaseException
    {
        public LinkNotFoundException()
        {
        }

        public LinkNotFoundException(IErrorContext context) : base(context)
        {
        }

        public LinkNotFoundException(string message) : base(message)
        {
        }

        public LinkNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
