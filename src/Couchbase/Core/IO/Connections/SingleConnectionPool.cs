using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections.Channels;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using BucketNotFoundException = Couchbase.Management.Buckets.BucketNotFoundException;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Legacy implementation of an <see cref="IConnectionPool"/> which only contains a single connection.
    /// </summary>
    internal sealed class SingleConnectionPool : ConnectionPoolBase
    {
        private readonly object _lock = new();
        private volatile IConnection? _connection;
        private TaskCompletionSource<IConnection>? _connecting;
        private volatile bool _disposed;

        /// <inheritdoc />
        public sealed override int Size => 1;

        /// <inheritdoc />
        public sealed override int MinimumSize
        {
            get => 1;
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public sealed override int MaximumSize
        {
            get => 1;
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public sealed override int PendingSends => 0;

        /// <inheritdoc />
        protected override bool IsDisposed => _disposed;

        /// <summary>
        /// Creates a new SingleConnectionPool.
        /// </summary>
        /// <param name="connectionInitializer">Handler for initializing new connections.</param>
        /// <param name="connectionFactory">Factory for creating new connections.</param>
        /// <param name="logger">The logger for logging.</param>
        /// <param name="bucketName">The bucket that each new connection selects, or null for none.</param>
        public SingleConnectionPool(IConnectionInitializer connectionInitializer,
            IConnectionFactory connectionFactory, ILogger<IConnectionPool> logger, string? bucketName = null)
            : base (connectionInitializer, connectionFactory, logger, bucketName)
        {
        }

        /// <inheritdoc />
        public override Task InitializeAsync(CancellationToken cancellationToken = default) =>
            ConnectAsync(cancellationToken);

        /// <inheritdoc />
        public override async Task SendAsync(IOperation operation, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                ThrowHelper.ThrowSocketNotAvailableException(nameof(SingleConnectionPool));
            }

            var connection = _connection;
            if (connection is null || connection.IsDead)
            {
                // A node that refused the bucket at its first connect has no connection yet, so this connects again.
                try
                {
                    connection = await ConnectAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (BucketNotFoundException)
                {
                    // The retry logic handles this type, and the next send connects again.
                    ThrowHelper.ThrowSocketNotAvailableException(nameof(SingleConnectionPool));
                }
            }

            await operation.SendAsync(connection!, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override IEnumerable<IConnection> GetConnections()
        {
            var connection = _connection;
            if (connection != null)
            {
                yield return connection;
            }
        }

        /// <inheritdoc />
        public override Task ScaleAsync(int delta)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Returns the live connection, or opens one. Callers that arrive during a connect wait for its result, so the
        /// pool opens one connection at a time.
        /// </summary>
        /// <remarks>
        /// The connect runs on the token of the caller that starts it. A failed connect is not kept, so the next call
        /// connects again.
        /// </remarks>
        private Task<IConnection> ConnectAsync(CancellationToken cancellationToken)
        {
            TaskCompletionSource<IConnection> connecting;
            lock (_lock)
            {
                if (_disposed)
                {
                    return Task.FromException<IConnection>(new SocketNotAvailableException(nameof(SingleConnectionPool)));
                }

                if (_connection is { IsDead: false } live)
                {
                    return Task.FromResult(live);
                }

                if (_connecting is not null)
                {
                    return _connecting.Task;
                }

                connecting = _connecting = new TaskCompletionSource<IConnection>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _ = RunConnectAsync(connecting, cancellationToken);
            return connecting.Task;
        }

        private async Task RunConnectAsync(TaskCompletionSource<IConnection> connecting,
            CancellationToken cancellationToken)
        {
            IConnection? connection = null;
            Exception? error = null;
            try
            {
                connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                error = e;
            }

            IConnection? replaced = null;
            var stored = false;
            lock (_lock)
            {
                _connecting = null;
                if (connection is not null && !_disposed)
                {
                    replaced = _connection;
                    _connection = connection;
                    stored = true;
                }
            }

            replaced?.Dispose();

            if (error is not null)
            {
                connecting.TrySetException(error);
            }
            else if (!stored)
            {
                // Dispose ran during the connect.
                connection!.Dispose();
                connecting.TrySetException(new SocketNotAvailableException(nameof(SingleConnectionPool)));
            }
            else
            {
                connecting.TrySetResult(connection!);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Does not wait for a connect in progress. A connection that opens after this is disposed and not kept.
        /// </remarks>
        public override void Dispose()
        {
            IConnection? connection;
            lock (_lock)
            {
                _disposed = true;
                connection = _connection;
                _connection = null;
            }

            connection?.Dispose();
        }
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
