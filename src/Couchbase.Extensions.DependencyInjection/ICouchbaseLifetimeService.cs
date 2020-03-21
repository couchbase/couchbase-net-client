using System.Threading.Tasks;

namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides a method to gracefully close the Couchbase connections
    /// during application shutdown.
    /// </summary>
    public interface ICouchbaseLifetimeService
    {
        /// <summary>
        /// Close all open Couchbase buckets and clusters.  If using the default
        /// implementations, this operation cannot be reversed without rebuilding
        /// the service provider.
        /// </summary>
        ValueTask CloseAsync();
    }
}
