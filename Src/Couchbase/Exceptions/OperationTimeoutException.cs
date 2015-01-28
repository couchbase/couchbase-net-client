using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Couchbase.Exceptions
{
    public class OperationTimeoutException : TimeoutException
    {
        public OperationTimeoutException(string message)
            : base(message)
        {
        }

        public OperationTimeoutException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public OperationTimeoutException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}