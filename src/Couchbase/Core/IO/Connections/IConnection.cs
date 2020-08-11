using System;
using System.Buffers;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Represents a TCP connection to a Couchbase Server instance.
    /// </summary>
    internal interface IConnection : IDisposable
    {
        /// <summary>
        /// Internal randomly generated connection ID.
        /// </summary>
        ulong ConnectionId { get; }

        /// <summary>
        /// True if the connection has been SASL authenticated.
        /// </summary>
        bool IsAuthenticated { get; set; }

        /// <summary>
        /// True if connection is using SSL
        /// </summary>
        bool IsSecure { get; }

        /// <summary>
        /// Gets a value indicating whether the underlying socket is connected to the remote host.
        /// </summary>
        /// <value>
        /// <c>true</c> if this socket is connected; otherwise, <c>false</c>.
        /// </value>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the remote hosts <see cref="EndPoint"/> that this <see cref="IConnection"/> is connected to.
        /// </summary>
        /// <value>
        /// The end point.
        /// </value>
        EndPoint EndPoint { get; }

        /// <summary>
        /// Gets the local endpoint for the connected socket.
        /// </summary>
        EndPoint LocalEndPoint { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is dead.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is dead; otherwise, <c>false</c>.
        /// </value>
        bool IsDead { get; }

        /// <summary>
        /// Gets the amount of time this connection has been idle.
        /// </summary>
        TimeSpan IdleTime { get; }

        /// <summary>
        /// Sends a request packet as an asynchronous operation.
        /// </summary>
        /// <param name="buffer">A memcached request buffer.</param>
        /// <param name="callback">The callback that will be fired after the operation is completed.</param>
        /// <param name="errorMap"><see cref="ErrorMap"/>, or null if not available.</param>
        Task SendAsync(ReadOnlyMemory<byte> buffer, Action<IMemoryOwner<byte>, ResponseStatus> callback, ErrorMap? errorMap = null);

        /// <summary>
        /// Closes the connection gracefully, waiting up to timeout for all in-flight operations
        /// to be completed, then disposes the connection.
        /// </summary>
        /// <param name="timeout">Time to wait for in-flight operations.</param>
        /// <returns>Task to observe for completion.</returns>
        ValueTask CloseAsync(TimeSpan timeout);
    }
}
