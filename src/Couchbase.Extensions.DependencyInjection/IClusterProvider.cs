using System;
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
    }
}
