using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Couchbase.IO.Operations.Errors;
using OpenTracing;

namespace Couchbase.IO
{
    /// <summary>
    /// Represents a TCP connection to a Couchbase Server instance.
    /// </summary>
    public interface IConnection : IDisposable
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
        /// Internal randomly generated connectio ID.
        /// </summary>
        ulong ConnectionId { get; }

        /// <summary>
        /// Gets the connection context identifier.
        /// </summary>
        /// <value>
        /// Connection context ID as a <see cref="string"/>.
        /// </value>
        string ContextId { get; }

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
        /// Gets the remove hosts <see cref="EndPoint"/> that this <see cref="Connection"/> is connected to.
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
        void SendAsync(byte[] buffer, Func<SocketAsyncState, Task> callback);

        void SendAsync(byte[] buffer, Func<SocketAsyncState, Task> callback, ISpan dispatchSpan, ErrorMap errorMap);

        /// <summary>
        /// Sends a request packet as an asynchronous operation; waiting for the reponse.
        /// </summary>
        /// <param name="request">A memcached request buffer.</param>
        /// <returns>A memcached response packet.</returns>
        byte[] Send(byte[] request);

        /// <summary>
        ///  Checks whether this <see cref="Connection"/> is currently being used to execute a request.
        /// </summary>
        /// <value>
        ///   <c>true</c> if if this <see cref="Connection"/> is in use; otherwise, <c>false</c>.
        /// </value>
        bool InUse { get; }

        /// <summary>
        /// Marks this <see cref="Connection"/> as used; meaning it cannot be disposed unless <see cref="InUse"/>
        /// is <c>false</c> or the <see cref="MaxCloseAttempts"/> has been reached.
        /// </summary>
        /// <param name="isUsed">if set to <c>true</c> [is used].</param>
        void MarkUsed(bool isUsed);

        /// <summary>
        /// Disposes this <see cref="Connection"/> if <see cref="InUse"/> is <c>false</c>; otherwise
        /// it will wait for the interval and attempt again up until the <see cref="MaxCloseAttempts"/>
        /// threshold is met or <see cref="InUse"/> is <c>false</c>.
        /// </summary>
        /// <param name="interval">The interval to wait between close attempts.</param>
        void CountdownToClose(uint interval);

        /// <summary>
        /// Gets or sets the maximum times that the client will check the <see cref="InUse"/>
        /// property before closing the connection.
        /// </summary>
        /// <value>
        /// The maximum close attempts.
        /// </value>
        int MaxCloseAttempts { get; set; }

        /// <summary>
        /// Gets the number of close attempts that this <see cref="Connection"/> has attemped.
        /// </summary>
        /// <value>
        /// The close attempts.
        /// </value>
        int CloseAttempts { get; }

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
        /// Authenticates this instance.
        /// </summary>
        void Authenticate();

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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
