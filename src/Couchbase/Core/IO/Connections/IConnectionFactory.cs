using System.Net;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Creates and connects an <see cref="IConnection"/>.
    /// </summary>
    internal interface IConnectionFactory
    {
        /// <summary>
        /// Creates and connects an <see cref="IConnection"/>.
        /// </summary>
        /// <param name="endPoint">Endpoint to connect.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The new <see cref="IConnection"/>.</returns>
        Task<IConnection> CreateAndConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken = default);
    }
}
