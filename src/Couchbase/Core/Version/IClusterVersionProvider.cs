using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.Version
{
    /// <summary>
    /// Provides version information about the cluster.
    /// </summary>
    /// <remarks>
    /// The implementation of this interface is typically obtained from <see cref="ICluster.ClusterServices"/>.
    /// </remarks>
    public interface IClusterVersionProvider
    {
        /// <summary>
        /// Gets the <see cref="ClusterVersion"/> from the currently connected cluster, if available.
        /// </summary>
        /// <returns>The <see cref="ClusterVersion"/>, or null if unavailable.</returns>
        ValueTask<ClusterVersion?> GetVersionAsync();

        /// <summary>
        /// Clear any cached value, getting a fresh value from the cluster on the next request.
        /// </summary>
        void ClearCache();
    }
}
