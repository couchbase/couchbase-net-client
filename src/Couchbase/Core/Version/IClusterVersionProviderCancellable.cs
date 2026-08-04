using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.Version
{
    /// <summary>
    /// Optional capability interface for <see cref="IClusterVersionProvider"/> implementations that can
    /// honor a <see cref="CancellationToken"/> for the underlying request. Adding a member directly to
    /// <see cref="IClusterVersionProvider"/> would be a binary-breaking change for any external
    /// implementer of that interface (e.g. one registered via <c>ClusterOptions.AddService</c>), so
    /// cancellation support is offered here instead and discovered via <c>is</c>/pattern matching - see
    /// <see cref="ClusterVersionProviderExtensions"/>.
    /// </summary>
    public interface IClusterVersionProviderCancellable : IClusterVersionProvider
    {
        /// <summary>
        /// Gets the <see cref="ClusterVersion"/> from the currently connected cluster, if available.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the underlying HTTP request.</param>
        /// <returns>The <see cref="ClusterVersion"/>, or null if unavailable.</returns>
        ValueTask<ClusterVersion?> GetVersionAsync(CancellationToken cancellationToken);
    }
}
