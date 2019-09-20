using System;

namespace Couchbase.Services.KeyValue
{
    public class CannotInsertValueException : KeyValueException
    {
        public CannotInsertValueException()
        {
        }

        public CannotInsertValueException(string message)
            : base(message)
        {
        }

        public CannotInsertValueException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
