using System;

namespace Couchbase
{
    public class ValueTooDeepException : KeyValueException
    {
        public ValueTooDeepException()
        {
        }

        public ValueTooDeepException(string message)
            : base(message)
        {
        }

        public ValueTooDeepException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
