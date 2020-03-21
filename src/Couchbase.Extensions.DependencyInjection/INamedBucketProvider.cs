using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Base interface for injecting specific buckets. Inherit an empty interface from this interface,
    /// and then use <see cref="ServiceCollectionExtensions.AddCouchbaseBucket{T}(IServiceCollection, string)"/>
    /// to register the interface in the <see cref="IServiceCollection"/>.
    /// </summary>
    public interface INamedBucketProvider
    {
        /// <summary>
        /// Name of the bucket.
        /// </summary>
        string BucketName { get; }

        /// <summary>
        /// Returns the a singleton instance of the bucket referenced by this interface.
        /// Do not dispose the bucket, it will be reused.
        /// </summary>
        /// <returns>The <see cref="IBucket"/>.</returns>
        ValueTask<IBucket> GetBucketAsync();
    }
}
