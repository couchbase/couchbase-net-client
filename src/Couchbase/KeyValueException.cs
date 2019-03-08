using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Couchbase.Core.IO.Operations;

namespace Couchbase
{
    public class KeyValueException : Exception
    {
        public KeyValueException()
        {
        }

        public KeyValueException(string message) : base(message)
        {
        }

        public KeyValueException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class CasMismatchException : KeyValueException
    {
        public CasMismatchException()
        {
        }

        public CasMismatchException(string message) : base(message)
        {
        }

        public CasMismatchException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
