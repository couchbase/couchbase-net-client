using System;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides access to a Couchbase cluster.
    /// </summary>
    public interface IClusterProvider : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Returns the Couchbase cluster.
        /// </summary>
        ValueTask<ICluster> GetClusterAsync();

        /// <summary>
        /// Returns the Couchbase cluster.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// If the <paramref name="cancellationToken"/> is canceled, the attempt to connect to the
        /// Couchbase cluster may not be canceled. Instead, only the wait for the result will be canceled.
        /// </remarks>
        ValueTask<ICluster> GetClusterAsync(CancellationToken cancellationToken)
#if NET6_0_OR_GREATER
        {
            // Backward compatibility fallback when not implemented on versions of .NET that support default interface methods.
            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<ICluster>(cancellationToken);
            }

            var result = GetClusterAsync();
            if (result.IsCompleted)
            {
                return result;
            }

            return new ValueTask<ICluster>(result.AsTask().WaitAsync(cancellationToken));
        }
#else
            ;
#endif
    }
}
