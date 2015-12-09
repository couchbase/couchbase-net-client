using System;

namespace Couchbase.IO
{
    /// <summary>
    /// Thrown when an available <see cref="IConnection"/> cannot be obtained from the <see cref="IConnectionPool"/> after n number of tries.
    /// </summary>
    public sealed class ConnectionUnavailableException : Exception
    {
        public ConnectionUnavailableException()
        {
        }

        public ConnectionUnavailableException(string message, params object[] args)
            : base(string.Format(message, args))
        {
        }

        public ConnectionUnavailableException(string message)
            : base(message)
        {
        }

        public ConnectionUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
