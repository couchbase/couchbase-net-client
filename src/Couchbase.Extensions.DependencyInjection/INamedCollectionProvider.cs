using System;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Microsoft.Extensions.DependencyInjection;

namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Base interface for injecting specific Couchbase collections.
    /// </summary>
    /// <remarks>
    /// Inherit an empty interface from this interface, and then use <see cref="IScopeBuilder.AddCollection{T}(string)"/>
    /// to register the interface in the <see cref="IServiceCollection"/>.
    /// </remarks>
    /// <example>
    /// <code>
    ///     services.AddCouchbaseBucket&lt;IMyBucket&gt;("my-bucket", builder => {
    ///         builder.AddDefaultCollection&lt;IMyDefaultCollection&gt;();
    ///         builder.AddScope("my-scope")
    ///             .AddCollection&lt;IMyCollection&gt;("my-collection");
    ///     });
    /// </code>
    /// </example>
    public interface INamedCollectionProvider
    {
        /// <summary>
        /// Name of the scope.
        /// </summary>
        string ScopeName { get; }

        /// <summary>
        /// Name of the collection.
        /// </summary>
        string CollectionName { get; }

        /// <summary>
        /// Returns the collection.
        /// </summary>
        /// <returns>The <see cref="ICouchbaseCollection" />.</returns>
        ValueTask<ICouchbaseCollection> GetCollectionAsync();
    }
}
