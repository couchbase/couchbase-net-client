using System;

namespace Couchbase
{
    /// <summary>
    /// Thrown when the IQueryClient cannot prepare a non-adhoc statement on the server.
    public sealed class PrepareStatementException : Exception
    {
        public PrepareStatementException(string message)
            : base(message)
        {
        }

        public PrepareStatementException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}

