using System;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Legacy.Errors;

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

        public ResponseStatus ResponseStatus { get; internal set; }

        public ErrorMap ErrorMap { get; internal set; }

        public static KeyValueException Create(ResponseStatus status, Exception innerException = null, string message = null, ErrorMap errorMap = null)
        {
            return new KeyValueException(message, innerException)
            {
                ErrorMap = errorMap,
                ResponseStatus = status
            };
        }

        //TODO Note this should include the ErrorMap and any other info
        public override string ToString()
        {
            return "K/V Error: " + ResponseStatus;
        }
    }

    //TODO for legacy remove later
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
