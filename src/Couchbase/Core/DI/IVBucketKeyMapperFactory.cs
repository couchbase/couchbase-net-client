using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Sharding;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates an <see cref="IKeyMapper"/> for VBuckets.
    /// </summary>
    internal interface IVBucketKeyMapperFactory
    {
        /// <summary>
        /// Creates an <see cref="IKeyMapper"/> for VBuckets.
        /// </summary>
        /// <param name="bucketConfig">Bucket configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <remarks>The new <see cref="IKeyMapper"/>.</remarks>
        Task<IKeyMapper> CreateAsync(BucketConfig bucketConfig, CancellationToken cancellationToken = default);
    }
}
