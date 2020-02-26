using System;

namespace Couchbase.Core.Exceptions.Analytics
{
    public class DataverseExistsException : CouchbaseException
    {
        public DataverseExistsException()
        {
        }

        public DataverseExistsException(IErrorContext context) : base(context)
        {
        }

        public DataverseExistsException(string message) : base(message)
        {
        }

        public DataverseExistsException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
