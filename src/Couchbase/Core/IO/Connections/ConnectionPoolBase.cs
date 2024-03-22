using System;
using System.Collections.Concurrent;
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

        private static readonly ConcurrentBag<WeakReference<ConnectionPoolBase>> _connectionPools = new();

        /// <summary>
        /// Add a connection pool to the list of active connection pools. We don't want to do this in the ConnectionPoolBase
        /// constructor because the constructor of the inherited class will not be complete yet. This will generally be the
        /// last method called by the inherited class constructor.
        /// </summary>
        /// <param name="connectionPool">Connection pool to track.</param>
        protected void TrackConnectionPool(ConnectionPoolBase connectionPool)
        {
            _connectionPools.Add(new WeakReference<ConnectionPoolBase>(connectionPool));
        }

        public static int GetSendQueueLength() => _connectionPools
            .Sum(p => p.TryGetTarget(out var connectionPool) ? connectionPool.PendingSends : 0);

        #endregion

        private readonly IConnectionInitializer _connectionInitializer;
        private readonly IConnectionFactory _connectionFactory;
        private readonly ILogger<IConnectionPool> _logger;

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
        /// Current bucket name passed to <see cref="SelectBucketAsync(string,CancellationToken)"/>.
        /// </summary>
        protected string? BucketName { get; set; }

        /// <summary>
        /// Creates a new ConnectionPoolBase.
        /// </summary>
        /// <param name="connectionInitializer">Handler for initializing new connections.</param>
        /// <param name="connectionFactory">Factory for creating new connections.</param>
        /// <param name="logger">The logger for logging.</param>
        protected ConnectionPoolBase(IConnectionInitializer connectionInitializer,
            IConnectionFactory connectionFactory, ILogger<IConnectionPool> logger)
        {
            _connectionInitializer = connectionInitializer ?? throw new ArgumentNullException(nameof(connectionInitializer));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger;
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
            var connection = await _connectionFactory
                .CreateAndConnectAsync(EndPoint, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await _connectionInitializer.InitializeConnectionAsync(connection, cancellationToken)
                    .ConfigureAwait(false);

                if (BucketName != null)
                {
                    await _connectionInitializer.SelectBucketAsync(connection, BucketName, cancellationToken)
                        .ConfigureAwait(false);
                }

                return connection;
            }
            catch(Exception e)
            {
                _logger.LogDebug(e, "Connection creation to {endpoint} failed.", EndPoint);

                // Be sure to cleanup the connection if bootstrap fails.
                // Use the synchronous dispose as we don't need to wait for in-flight operations to complete.
                connection.Dispose();
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task SelectBucketAsync(string name, CancellationToken cancellationToken = default)
        {
            await using ((await FreezePoolAsync(cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
            {
                var tasks = GetConnections()
                    .Select(connection => Task.Run(async () =>
                    {
                        try
                        {
                            await _connectionInitializer.SelectBucketAsync(connection, name, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch
                        {
                            // Be sure to cleanup the connection if bootstrap fails.
                            // Use the synchronous dispose as we don't need to wait for in-flight operations to complete.
                            connection.Dispose();
                            throw;
                        }
                    }, cancellationToken))
                    .ToList();

                await Task.WhenAll(tasks).ConfigureAwait(false);

                BucketName = name;
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
