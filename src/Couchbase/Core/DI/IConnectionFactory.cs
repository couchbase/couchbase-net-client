using System.Net;
using System.Threading.Tasks;
using Couchbase.Core.IO;

#nullable enable

namespace Couchbase.Core.DI
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
        /// <returns>The new <see cref="IConnection"/>.</returns>
        Task<IConnection> CreateAndConnectAsync(IPEndPoint endPoint);
    }
}
