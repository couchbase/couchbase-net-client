using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
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
        /// The Socket used for IO.
        /// </summary>
        Socket Socket { get; }

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
        /// Gets a value indicating whether the underlying socket is connected to the remopte host.
        /// </summary>
        /// <value>
        /// <c>true</c> if this socket is connected; otherwise, <c>false</c>.
        /// </value>
        bool IsConnected { get; }

        /// <summary>
        /// Gets the remove hosts <see cref="EndPoint"/> that this <see cref="IConnection"/> is connected to.
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
        bool IsDead { get; set; }

        /// <summary>
        /// Gets the timestamp of the last activity.
        /// </summary>
        DateTime? LastActivity { get; }

        /// <summary>
        /// Sends a request packet as an asynchronous operation.
        /// </summary>
        /// <param name="buffer">A memcached request buffer.</param>
        /// <param name="callback">The callback that will be fired after the operation is completed.</param>
        Task SendAsync(ReadOnlyMemory<byte> buffer, Func<SocketAsyncState, Task> callback);

        Task SendAsync(ReadOnlyMemory<byte> buffer, Func<SocketAsyncState, Task> callback, ErrorMap? errorMap);

        /// <summary>
        ///  Checks whether this <see cref="IConnection"/> is currently being used to execute a request.
        /// </summary>
        /// <value>
        ///   <c>true</c> if if this <see cref="IConnection"/> is in use; otherwise, <c>false</c>.
        /// </value>
        bool InUse { get; }

        /// <summary>
        /// Marks this <see cref="IConnection"/> as used; meaning it cannot be disposed unless <see cref="InUse"/>
        /// is <c>false</c> or the <see cref="MaxCloseAttempts"/> has been reached.
        /// </summary>
        /// <param name="isUsed">if set to <c>true</c> [is used].</param>
        void MarkUsed(bool isUsed);

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
        /// </value>
        bool IsDisposed { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is shutting down.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has shutdown; otherwise, <c>false</c>.
        /// </value>
        bool HasShutdown { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the connection has been checked for enhanced authentication.
        /// </summary>
        /// <value>
        /// <c>true</c> if the connection has been checked for enhanced authentication; otherwise, <c>false</c>.
        /// </value>
        bool CheckedForEnhancedAuthentication { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the connection must enable server features.
        /// </summary>
        /// <value>
        /// <c>true</c> if the connection must enable server features; otherwise, <c>false</c>.
        /// </value>
        bool MustEnableServerFeatures { get; set; }
    }
}
