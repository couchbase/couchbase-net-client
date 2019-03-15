using System;

namespace Couchbase
{
    public class DeltaRangeException : KeyValueException
    {
        public DeltaRangeException()
        {
        }

        public DeltaRangeException(string message)
            : base(message)
        {
        }

        public DeltaRangeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
