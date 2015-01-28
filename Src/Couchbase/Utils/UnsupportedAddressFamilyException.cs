using System;
using System.Runtime.Serialization;

namespace Couchbase.Utils
{
    public class UnsupportedAddressFamilyException : Exception
    {
        public UnsupportedAddressFamilyException()
        {
        }

        public UnsupportedAddressFamilyException(string message)
            : base(message)
        {
        }

        public UnsupportedAddressFamilyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected UnsupportedAddressFamilyException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}