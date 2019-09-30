using System;

namespace Couchbase.KeyValue
{
    public class NumRangeException : KeyValueException
    {
        public NumRangeException()
        {
        }

        public NumRangeException(string message)
            : base(message)
        {
        }

        public NumRangeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
