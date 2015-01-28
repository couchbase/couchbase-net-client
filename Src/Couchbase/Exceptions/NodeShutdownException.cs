using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Couchbase.Exceptions
{
    /// <summary>
    /// Thrown when an application thread tries to use a CouchbaseNode that has been shutdown.
    /// </summary>
    public class NodeShutdownException : ObjectDisposedException
    {
        public NodeShutdownException(string message)
            : base(message)
        {
        }

        public NodeShutdownException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public NodeShutdownException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}