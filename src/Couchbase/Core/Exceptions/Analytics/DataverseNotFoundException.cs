using System;

namespace Couchbase.Core.Exceptions.Analytics
{
    public class DataverseNotFoundException : CouchbaseException
    {
        public DataverseNotFoundException()
        {
        }

        public DataverseNotFoundException(IErrorContext context) : base(context)
        {
        }

        public DataverseNotFoundException(string message) : base(message)
        {
        }

        public DataverseNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
