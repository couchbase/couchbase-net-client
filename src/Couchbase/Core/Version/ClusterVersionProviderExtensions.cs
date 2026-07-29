using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.Version
{
    /// <summary>
    /// Extension methods for <see cref="IClusterVersionProvider"/>.
    /// </summary>
    public static class ClusterVersionProviderExtensions
    {
        /// <summary>
        /// Gets the <see cref="ClusterVersion"/> from the currently connected cluster, if available,
        /// honoring <paramref name="cancellationToken"/> when <paramref name="provider"/> implements
        /// <see cref="IClusterVersionProviderCancellable"/>. Implementations that don't support
        /// cancellation ignore the token rather than faking it - there's no way to actually abort work
        /// they never exposed a token for.
        /// </summary>
        /// <param name="provider">The provider to query.</param>
        /// <param name="cancellationToken">Cancellation token for the underlying request, honored only
        /// if <paramref name="provider"/> implements <see cref="IClusterVersionProviderCancellable"/>.</param>
        /// <returns>The <see cref="ClusterVersion"/>, or null if unavailable.</returns>
        public static ValueTask<ClusterVersion?> GetVersionAsync(this IClusterVersionProvider provider,
            CancellationToken cancellationToken) =>
            provider is IClusterVersionProviderCancellable cancellable
                ? cancellable.GetVersionAsync(cancellationToken)
                : provider.GetVersionAsync();
    }
}
