using System;

namespace Couchbase.Core.Exceptions.KeyValue
{
    public class ValueToolargeException : CouchbaseException
    {
        public ValueToolargeException()
        {
        }

        public ValueToolargeException(IErrorContext context) : base(context)
        {
        }

        public ValueToolargeException(string message) : base(message)
        {
        }

        public ValueToolargeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
