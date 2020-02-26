using System;

namespace Couchbase.Core.Exceptions.Analytics
{
    public class DatasetExistsException : CouchbaseException
    {
        public DatasetExistsException()
        {
        }

        public DatasetExistsException(IErrorContext context) : base(context)
        {
        }

        public DatasetExistsException(string message) : base(message)
        {
        }

        public DatasetExistsException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
