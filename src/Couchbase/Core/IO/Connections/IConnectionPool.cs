using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Represents a pool of TCP connections to a Couchbase Server node.
    /// </summary>
    internal interface IConnectionPool : IDisposable
    {
        /// <summary>
        /// The <see cref="HostEndpointWithPort"/> of the server that the <see cref="IConnection"/>s are connected to.
        /// </summary>
        HostEndpointWithPort EndPoint { get; }

        /// <summary>
        /// Current size of the pool.
        /// </summary>
        public int Size { get; }

        /// <summary>
        /// Minimum number of connections in the pool.
        /// </summary>
        public int MinimumSize { get; }

        /// <summary>
        /// Maximum number of connections in the pool.
        /// </summary>
        public int MaximumSize { get; }

        /// <summary>
        /// The number of pending sends on the connection pool.
        /// </summary>
        public int PendingSends { get; }

        /// <summary>
        /// Initialize the connection pool, opening initial connections and generally preparing the pool for use.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>\
        /// <remarks>Task to observe for completion.</remarks>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Send an operation via a connection in the pool.
        /// </summary>
        /// <param name="op"><see cref="IOperation"/> to send.</param>
        /// <param name="cancellationToken">Cancellation token which cancels the send. The operation is unaffected if cancelled.</param>
        /// <returns>Task to observe for completion.</returns>
        /// <remarks>
        /// Completion of the returned task indicates that the operation has been either sent or queued to be sent.
        /// The operation will be marked as complete when a response is received.
        /// </remarks>
        Task SendAsync(IOperation op, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests that the connections in the pool be frozen, with no connections being added or removed.
        /// </summary>
        /// <returns>An <seealso cref="IAsyncDisposable"/> which releases the freeze when disposed.</returns>
        /// <remarks>
        /// Should be overriden by any derived class which supports rescaling connections.
        /// </remarks>
        ValueTask<IAsyncDisposable> FreezePoolAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Selects the bucket for all connections on the pool.
        /// </summary>
        /// <param name="name">The bucket name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task SelectBucketAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a snapshot in time list of the current connections in the pool.
        /// </summary>
        /// <returns>The current connections in the pool.</returns>
        IEnumerable<IConnection> GetConnections();

        /// <summary>
        /// Scale the pool up or down by a certain amount.
        /// </summary>
        /// <param name="delta">Amount to scale the pool.</param>
        /// <returns>Task to observer for completion.</returns>
        /// <remarks>
        /// It is assumed that the caller has already frozen the pool before calling ScalePool.
        /// Not freezing first so will have unexpected results.
        /// </remarks>
        Task ScaleAsync(int delta);
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
