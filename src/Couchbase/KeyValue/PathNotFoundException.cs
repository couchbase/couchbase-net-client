using System;

namespace Couchbase.KeyValue
{
    public class PathNotFoundException : KeyValueException
    {
        public PathNotFoundException()
        {
        }

        public PathNotFoundException(string message)
            : base(message)
        {
        }

        public PathNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
