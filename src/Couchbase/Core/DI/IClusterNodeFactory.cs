using System.Net;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates a <see cref="IClusterNode"/>.
    /// </summary>
    internal interface IClusterNodeFactory
    {
        /// <summary>
        /// Create and connect to a <see cref="IClusterNode"/>.
        /// </summary>
        /// <param name="endPoint"><see cref="IPEndPoint"/> of the node.</param>
        /// <returns>The <seealso cref="IClusterNode"/>.</returns>
        Task<IClusterNode> CreateAndConnectAsync(IPEndPoint endPoint);
    }
}
