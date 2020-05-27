using System.Threading;
using System.Threading.Tasks;
using Couchbase.Management.Buckets;

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
        /// <param name="endPoint"><see cref="HostEndpoint"/> of the node.</param>
        /// <param name="bucketType"></param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The <seealso cref="IClusterNode"/> type.</returns>
        Task<IClusterNode> CreateAndConnectAsync(HostEndpoint endPoint, BucketType bucketType, CancellationToken cancellationToken = default);
    }
}
