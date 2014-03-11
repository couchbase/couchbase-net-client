using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Couchbase.Exceptions
{
    /// <summary>
    /// Thrown when the timeout expires while waiting for a socket
    /// from the pool. The default expiry is 100ms. In general, the
    /// more sockets you have configured the lower this number can be;
    /// if you go to low, then you will begin receiving the exceptions.
    /// This value can be configured by the queueTimeout property in
    /// the socketPool section of the App.Config.
    /// </summary>
    public class QueueTimeoutException : TimeoutException
    {
        public QueueTimeoutException(string message)
            : base(message)
        {
        }

        public QueueTimeoutException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public QueueTimeoutException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}