using System;

namespace Couchbase
{
    public class TimeoutException : Exception
    {
        public TimeoutException()
        {
        }

        public TimeoutException(string message) : base(message)
        {
        }

        public TimeoutException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
