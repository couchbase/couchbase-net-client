using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Connections.Channels;
using Couchbase.Core.IO.Operations;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Legacy implementation of an <see cref="IConnectionPool"/> which only contains a single connection.
    /// </summary>
    internal sealed class SingleConnectionPool : ConnectionPoolBase
    {
        private IConnection? _connection;

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

        /// <summary>
        /// Creates a new SingleConnectionPool.
        /// </summary>
        /// <param name="connectionInitializer">Handler for initializing new connections.</param>
        /// <param name="connectionFactory">Factory for creating new connections.</param>
        /// <param name="logger">The logger for logging.</param>
        public SingleConnectionPool(IConnectionInitializer connectionInitializer,
            IConnectionFactory connectionFactory, ILogger<IConnectionPool> logger)
            : base (connectionInitializer, connectionFactory, logger)
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
            if (_connection == null)
            {
                throw new InvalidOperationException($"${nameof(SingleConnectionPool)} is not initialized.");
            }

            await CheckConnectionAsync().ConfigureAwait(false);

            await operation.SendAsync(_connection, cancellationToken).ConfigureAwait(false);
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
                await InitializeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
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
