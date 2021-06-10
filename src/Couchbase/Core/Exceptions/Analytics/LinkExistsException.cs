using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Core.Exceptions.Analytics
{
    public class LinkExistsException : CouchbaseException
    {
        public LinkExistsException()
        {
        }

        public LinkExistsException(IErrorContext context) : base(context)
        {
        }

        public LinkExistsException(string message) : base(message)
        {
        }

        public LinkExistsException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
