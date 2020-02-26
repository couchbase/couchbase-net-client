using System;

namespace Couchbase.Core.Exceptions.Analytics
{
    public class JobQueueFullException : CouchbaseException
    {
        public JobQueueFullException()
        {
        }

        public JobQueueFullException(IErrorContext context) : base(context)
        {
        }

        public JobQueueFullException(string message) : base(message)
        {
        }

        public JobQueueFullException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
