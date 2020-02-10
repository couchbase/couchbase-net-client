using System.Net;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Interface for initializing a newly connected <see cref="IConnection"/>.
    /// Primarily used by <see cref="IConnectionPool"/> implementations to support
    /// creating new connections as needed.
    /// </summary>
    /// <seealso cref="ClusterNode"/>.
    internal interface IConnectionInitializer
    {
        /// <summary>
        /// <see cref="IPEndPoint"/> of the node being connected to.
        /// </summary>
        IPEndPoint EndPoint { get; }

        /// <summary>
        /// Initializes and authenticates a new <see cref="IConnection"/>.
        /// </summary>
        /// <param name="connection">A newly connected <see cref="IConnection"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>Task to observe for completion.</remarks>
        Task InitializeConnectionAsync(IConnection connection, CancellationToken cancellationToken = default);

        /// <summary>
        /// Selects the active bucket on an <see cref="IConnection"/>.
        /// </summary>
        /// <param name="connection">An active and initialized <see cref="IConnection"/>.</param>
        /// <param name="name">Name of the bucket.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>Task to observe for completion.</remarks>
        Task SelectBucketAsync(IConnection connection, string name, CancellationToken cancellationToken = default);
    }
}
