using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Sharding;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates a new <see cref="KetamaKeyMapper" />
    /// </summary>
    internal interface IKetamaKeyMapperFactory
    {
        /// <summary>
        /// Creates a new <see cref="KetamaKeyMapper" />
        /// </summary>
        /// <param name="bucketConfig">The <see cref="BucketConfig"/>.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The new <see cref="KetamaKeyMapper"/>.</returns>
        /// <exception cref="InvalidOperationException">IP endpoint lookup failed.</exception>
        Task<KetamaKeyMapper> CreateAsync(BucketConfig bucketConfig, CancellationToken cancellationToken = default);
    }
}
