using System;

namespace Couchbase.Core.Exceptions.Analytics
{
    public class DatasetNotFoundException : CouchbaseException
    {
        public DatasetNotFoundException()
        {
        }

        public DatasetNotFoundException(IErrorContext context) : base(context)
        {
        }

        public DatasetNotFoundException(string message) : base(message)
        {
        }

        public DatasetNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
