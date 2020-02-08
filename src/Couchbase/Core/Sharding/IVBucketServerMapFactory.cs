using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.Sharding
{
    /// <summary>
    /// Creates a new <see cref="VBucketServerMap"/> from a <see cref="VBucketServerMapDto"/>.
    /// </summary>
    internal interface IVBucketServerMapFactory
    {
        /// <summary>
        /// Creates a new <see cref="VBucketServerMap"/> from a <see cref="VBucketServerMapDto"/>.
        /// </summary>
        /// <param name="serverMapDto">The source <see cref="VBucketServerMapDto"/>.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The new <see cref="VBucketServerMap"/>.</returns>
        Task<VBucketServerMap> CreateAsync(VBucketServerMapDto serverMapDto,
            CancellationToken cancellationToken = default);
    }
}
