using System;
using System.Net.Sockets;

namespace Couchbase.IO
{
    /// <summary>
    /// Represents a TCP connection to a Couchbase Server instance.
    /// </summary>
    internal interface IConnection : IDisposable
    {
        /// <summary>
        /// The Socket used for IO.
        /// </summary>
        Socket Socket { get; }

        /// <summary>
        /// Unique identifier for this connection.
        /// </summary>
        Guid Identity { get; }

        /// <summary>
        /// True if the connection has been SASL authenticated.
        /// </summary>
        bool IsAuthenticated { get; set; }
    }
}
