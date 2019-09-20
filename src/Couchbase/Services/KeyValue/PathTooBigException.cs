using System;

namespace Couchbase.Services.KeyValue
{
    public class PathTooBigException : KeyValueException
    {
        public PathTooBigException()
        {
        }

        public PathTooBigException(string message)
            : base(message)
        {
        }

        public PathTooBigException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
