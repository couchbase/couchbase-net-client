using System;

namespace Couchbase
{
    public class DurabilityExceptions : KeyValueException
    {
        public DurabilityExceptions()
        {
        }

        public DurabilityExceptions(string message) : base(message)
        {
        }

        public DurabilityExceptions(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
