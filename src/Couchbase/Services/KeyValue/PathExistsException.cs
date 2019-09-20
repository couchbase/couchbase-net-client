using System;

namespace Couchbase.Services.KeyValue
{
    public class PathExistsException : KeyValueException
    {
        public PathExistsException()
        {
        }

        public PathExistsException(string message)
            : base(message)
        {
        }

        public PathExistsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
