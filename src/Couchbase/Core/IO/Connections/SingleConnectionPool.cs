using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Operations;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Legacy implementation of an <see cref="IConnectionPool"/> which only contains a single connection.
    /// </summary>
    internal class SingleConnectionPool : ConnectionPoolBase
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
        public SingleConnectionPool(IConnectionInitializer connectionInitializer,
            IConnectionFactory connectionFactory)
            : base (connectionInitializer, connectionFactory)
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
