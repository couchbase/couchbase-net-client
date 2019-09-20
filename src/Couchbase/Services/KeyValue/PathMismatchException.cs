using System;

namespace Couchbase.Services.KeyValue
{
    public class PathMismatchException : KeyValueException
    {
        public PathMismatchException()
        {
        }

        public PathMismatchException(string message)
            : base(message)
        {
        }

        public PathMismatchException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
