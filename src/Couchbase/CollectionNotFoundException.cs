
using System;

namespace Couchbase
{
    public class CollectionNotFoundException : Exception
    {
        public CollectionNotFoundException()
        {
        }

        public CollectionNotFoundException(string message) : base(message)
        {
        }

        public CollectionNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
