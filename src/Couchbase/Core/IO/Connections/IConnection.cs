using System;
using System.Buffers;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
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
        /// Unique identifier for the connection.
        /// </summary>
        string ContextId { get; }

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
        /// Features supported by the connection.
        /// </summary>
        ServerFeatureSet ServerFeatures { get; set; }

        /// <summary>
        /// Sends a request packet as an asynchronous operation.
        /// </summary>
        /// <param name="buffer">A memcached request buffer.</param>
        /// <param name="operation">Operation being sent which will receive the completion notification.</param>
        /// <param name="cancellationToken">Cancellation token which cancels the send. The operation is unaffected if cancelled.</param>
        /// <remarks>
        /// Completion of the returned task indicates that the operation has been sent on the wire.
        /// The operation will be marked as complete when a response is received.
        /// </remarks>
        ValueTask SendAsync(ReadOnlyMemory<byte> buffer, IOperation operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the connection gracefully, waiting up to timeout for all in-flight operations
        /// to be completed, then disposes the connection.
        /// </summary>
        /// <param name="timeout">Time to wait for in-flight operations.</param>
        /// <returns>Task to observe for completion.</returns>
        ValueTask CloseAsync(TimeSpan timeout);

        /// <summary>
        /// Add tags related to this connection to a tracing span.
        /// </summary>
        /// <param name="span">The tracing span to update.</param>
        void AddTags(IRequestSpan span);
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
