using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Sharding;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Default implementation of <see cref="IVBucketKeyMapperFactory"/>.
    /// </summary>
    internal class VBucketKeyMapperFactory : IVBucketKeyMapperFactory
    {
        private readonly IVBucketServerMapFactory _vBucketServerMapFactory;
        private readonly IVBucketFactory _vBucketFactory;

        public VBucketKeyMapperFactory(IVBucketServerMapFactory vBucketServerMapFactory,
            IVBucketFactory vBucketFactory)
        {
            _vBucketServerMapFactory = vBucketServerMapFactory ?? throw new ArgumentNullException(nameof(vBucketServerMapFactory));
            _vBucketFactory = vBucketFactory ?? throw new ArgumentNullException(nameof(vBucketFactory));
        }

        /// <inheritdoc />
        public async Task<IKeyMapper> CreateAsync(BucketConfig bucketConfig,
            CancellationToken cancellationToken = default) =>
            new VBucketKeyMapper(
                bucketConfig,
                await _vBucketServerMapFactory.CreateAsync(bucketConfig.VBucketServerMap, cancellationToken).ConfigureAwait(false),
                _vBucketFactory);
    }
}
