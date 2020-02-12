using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Base class for implementations of <see cref="IConnectionPool"/>.
    /// </summary>
    internal abstract class ConnectionPoolBase : IConnectionPool
    {
        private readonly IConnectionInitializer _connectionInitializer;
        private readonly IConnectionFactory _connectionFactory;

        /// <inheritdoc />
        public IPEndPoint EndPoint => _connectionInitializer.EndPoint;

        /// <summary>
        /// Current bucket name passed to <see cref="SelectBucketAsync(string,CancellationToken)"/>.
        /// </summary>
        protected string? BucketName { get; set; }

        /// <summary>
        /// Creates a new ConnectionPoolBase.
        /// </summary>
        /// <param name="connectionInitializer">Handler for initializing new connections.</param>
        /// <param name="connectionFactory">Factory for creating new connections.</param>
        protected ConnectionPoolBase(IConnectionInitializer connectionInitializer,
            IConnectionFactory connectionFactory)
        {
            _connectionInitializer = connectionInitializer ?? throw new ArgumentNullException(nameof(connectionInitializer));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
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
            var connection = await _connectionFactory.CreateAndConnectAsync(EndPoint, cancellationToken)
                .ConfigureAwait(false);

            await _connectionInitializer.InitializeConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

            if (BucketName != null)
            {
                await _connectionInitializer.SelectBucketAsync(connection, BucketName, cancellationToken).ConfigureAwait(false);
            }

            return connection;
        }

        /// <inheritdoc />
        public virtual async Task SelectBucketAsync(string name, CancellationToken cancellationToken = default)
        {
            await using ((await FreezePoolAsync(cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
            {
                var tasks = GetConnections()
                    .Select(connection =>
                        _connectionInitializer.SelectBucketAsync(connection, name, cancellationToken))
                    .ToList();

                await Task.WhenAll(tasks).ConfigureAwait(false);

                BucketName = name;
            }
        }

        /// <summary>
        /// Requests that the connections in the pool be frozen, with no connections being added or removed.
        /// </summary>
        /// <returns>An <seealso cref="IAsyncDisposable"/> which releases the freeze when disposed.</returns>
        /// <remarks>
        /// Should be overriden by any derived class which supports rescaling connections.
        /// </remarks>
        protected virtual ValueTask<IAsyncDisposable> FreezePoolAsync(CancellationToken cancellationToken = default) =>
            new ValueTask<IAsyncDisposable>(NullAsyncDisposable.Instance);

        /// <inheritdoc />
        public abstract Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract Task SendAsync(IOperation operation, CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract IEnumerable<IConnection> GetConnections();

        /// <inheritdoc />
        public abstract void Dispose();
    }
}
