using System;

namespace Couchbase
{
    public class PathInvalidException : KeyValueException
    {
        public PathInvalidException()
        {
        }

        public PathInvalidException(string message)
            : base(message)
        {
        }

        public PathInvalidException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
