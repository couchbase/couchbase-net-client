using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Base class for implementations of <see cref="IConnectionPool"/>.
    /// </summary>
    internal abstract class ConnectionPoolBase : IConnectionPool
    {
        #region Metrics

        private static readonly WeakInstanceRegistry<ConnectionPoolBase> _connectionPools = new();

        private long _trackingId;

        /// <summary>
        /// Add a connection pool to the list of active connection pools. We don't want to do this in the ConnectionPoolBase
        /// constructor because the constructor of the inherited class will not be complete yet. This will generally be the
        /// last method called by the inherited class constructor.
        /// </summary>
        /// <param name="connectionPool">Connection pool to track.</param>
        protected void TrackConnectionPool(ConnectionPoolBase connectionPool)
        {
            connectionPool._trackingId = _connectionPools.Add(connectionPool);
        }

        /// <summary>
        /// Remove this connection pool from the list of active connection pools. Inheritors must call this from
        /// <see cref="Dispose"/>, otherwise the pool is only untracked once it has been garbage collected.
        /// </summary>
        protected void UntrackConnectionPool()
        {
            _connectionPools.Remove(_trackingId);
        }

        public static int GetSendQueueLength() => GetSendQueueLength(_connectionPools);

        /// <summary>
        /// Computes the send queue length over an explicit registry. Exists so that unit tests can exercise
        /// the aggregation against a registry they own; the process-wide one cannot be asserted on because
        /// test classes running in parallel create pools of their own.
        /// </summary>
        internal static int GetSendQueueLength(WeakInstanceRegistry<ConnectionPoolBase> registry) => registry
            .EnumerateLive()
            .Sum(static p => p.PendingSends);

        /// <summary>
        /// For UNIT TESTING ONLY. Whether this pool is still in the set tracked for diagnostics. Membership
        /// of a specific instance is unaffected by pools which other tests create in parallel.
        /// </summary>
        internal bool IsTrackedForDiagnostics => _connectionPools.EnumerateLive().Contains(this);

        #endregion

        private readonly IConnectionInitializer _connectionInitializer;
        private readonly IConnectionFactory _connectionFactory;
        private readonly ILogger<IConnectionPool> _logger;
        private volatile bool _hasConnected;
        private volatile bool _connectFailed;

        /// <inheritdoc />
        public bool IsAwaitingFirstConnection => !_hasConnected;

        /// <inheritdoc />
        public bool HasFailedFirstConnect => !_hasConnected && _connectFailed;

        /// <summary>
        /// Tells a connect that fails during a dispose from a failed connect. The base returns false, because a pool
        /// that cancels the connect token on dispose needs no other check. The DataFlow pool does not cancel its first round, so a dispose there can count as failed.
        /// </summary>
        protected virtual bool IsDisposed => false;

        /// <inheritdoc />
        public HostEndpointWithPort EndPoint => _connectionInitializer.EndPoint;

        /// <inheritdoc />
        public abstract int Size { get; }

        /// <inheritdoc />
        public abstract int MinimumSize { get; set; }

        /// <inheritdoc />
        public abstract int MaximumSize { get; set; }

        /// <inheritdoc />
        public abstract int PendingSends { get; }

        /// <summary>
        /// The bucket that each new connection selects, or null when the connections select no bucket.
        /// A bucket node gives it at creation, and it never changes.
        /// </summary>
        protected string? BucketName { get; }

        /// <summary>
        /// Creates a new ConnectionPoolBase.
        /// </summary>
        /// <param name="connectionInitializer">Handler for initializing new connections.</param>
        /// <param name="connectionFactory">Factory for creating new connections.</param>
        /// <param name="logger">The logger for logging.</param>
        /// <param name="bucketName">The bucket that each new connection selects, or null for none.</param>
        protected ConnectionPoolBase(IConnectionInitializer connectionInitializer,
            IConnectionFactory connectionFactory, ILogger<IConnectionPool> logger, string? bucketName = null)
        {
            _connectionInitializer = connectionInitializer ?? throw new ArgumentNullException(nameof(connectionInitializer));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger;
            BucketName = bucketName;
        }

        /// <summary>
        /// Helper method which creates and initializes a new <see cref="IConnection"/> when needed by the pool.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The new connection.</returns>
        /// <remarks>
        /// The connection will be initialized. If <see cref="BucketName"/> is not null the initial bucket will be selected automatically.
        /// </remarks>
        protected async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
        {
            IConnection? connection = null;
            try
            {
                connection = await _connectionFactory
                    .CreateAndConnectAsync(EndPoint, cancellationToken)
                    .ConfigureAwait(false);

                await _connectionInitializer.InitializeConnectionAsync(connection, cancellationToken)
                    .ConfigureAwait(false);

                if (BucketName != null)
                {
                    await _connectionInitializer.SelectBucketAsync(connection, BucketName, cancellationToken)
                        .ConfigureAwait(false);
                }

                _hasConnected = true;
                return connection;
            }
            catch(Exception e)
            {
                // A cancel can surface as a socket timeout, so the token decides, not the exception type.
                if (!cancellationToken.IsCancellationRequested && !IsDisposed)
                {
                    _connectFailed = true;
                }

                if (connection is not null)
                {
                    _logger.LogDebug(e, "Connection creation to {endpoint} failed.", EndPoint);

                    // Be sure to cleanup the connection if bootstrap fails.
                    // Use the synchronous dispose as we don't need to wait for in-flight operations to complete.
                    connection.Dispose();
                }

                throw;
            }
        }

        /// <inheritdoc />
        public virtual ValueTask<IAsyncDisposable> FreezePoolAsync(CancellationToken cancellationToken = default) =>
            new ValueTask<IAsyncDisposable>(NullAsyncDisposable.Instance);

        /// <inheritdoc />
        public abstract Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract Task SendAsync(IOperation operation, CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public virtual async Task<bool> TrySendImmediatelyAsync(IOperation op, CancellationToken cancellationToken = default)
        {
            await SendAsync(op, cancellationToken).ConfigureAwait(false);

            return false;
        }

        /// <inheritdoc />
        public abstract IEnumerable<IConnection> GetConnections();

        /// <inheritdoc />
        public abstract Task ScaleAsync(int delta);

        /// <inheritdoc />
        public abstract void Dispose();
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
