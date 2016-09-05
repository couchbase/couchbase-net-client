using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Annotations;

namespace Couchbase
{
    /// <summary>
    /// Thrown when the client cannot find a healthy server to execute an operation on. This
    /// could temporarily happen during a swap/failover/rebalance situation. The calling code
    /// could decide to retry the operation after handling this exception.
    /// </summary>
    public sealed class ServerUnavailableException : Exception
    {
        public ServerUnavailableException()
        {
        }

        public ServerUnavailableException(string message)
            : base(message)
        {
        }

        public ServerUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#if NET45
        public ServerUnavailableException([NotNull] SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}
