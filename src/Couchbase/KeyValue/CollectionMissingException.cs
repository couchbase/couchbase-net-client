using System;

namespace Couchbase.KeyValue
{
    public class CollectionMissingException : Exception
    {
        public CollectionMissingException()
        {
        }

        public CollectionMissingException(string message) : base(message)
        {
        }

        public CollectionMissingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
