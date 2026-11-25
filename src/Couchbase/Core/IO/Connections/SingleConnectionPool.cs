using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
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
        private IConnection? _connection;
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
        public override async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);;
        }

        /// <inheritdoc />
        public override async Task SendAsync(IOperation operation, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                ThrowHelper.ThrowSocketNotAvailableException(nameof(SingleConnectionPool));
            }

            // A node that refused the bucket at its first connect has no connection yet, so this connects again.
            await CheckConnectionAsync().ConfigureAwait(false);

            await operation.SendAsync(_connection!, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override IEnumerable<IConnection> GetConnections()
        {
            if (_connection != null)
            {
                yield return _connection;
            }
        }

        /// <inheritdoc />
        public override Task ScaleAsync(int delta)
        {
            throw new NotSupportedException();
        }

        private async ValueTask CheckConnectionAsync()
        {
            if (_connection?.IsDead ?? true)
            {
                try
                {
                    await InitializeAsync().ConfigureAwait(false);
                }
                catch (BucketNotFoundException)
                {
                    // The retry logic handles this type, and the next send connects again.
                    ThrowHelper.ThrowSocketNotAvailableException(nameof(SingleConnectionPool));
                }
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _disposed = true;
            _connection?.Dispose();
            _connection = null;
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
