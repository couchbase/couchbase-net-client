using System;
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
        private readonly IVBucketFactory _vBucketFactory;

        public VBucketKeyMapperFactory(IVBucketFactory vBucketFactory)
        {
            _vBucketFactory = vBucketFactory ?? throw new ArgumentNullException(nameof(vBucketFactory));
        }

        /// <inheritdoc />
        public IKeyMapper Create(BucketConfig bucketConfig) =>
            new VBucketKeyMapper(bucketConfig, _vBucketFactory);
    }
}
