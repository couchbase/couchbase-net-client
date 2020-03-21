using System;
using System.Threading.Tasks;
using Couchbase.Core;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    /// <summary>
    /// Base implementation for <see cref="INamedBucketProvider"/>, used as the base
    /// class by <see cref="NamedBucketProxyGenerator"/>.
    /// </summary>
    internal abstract class NamedBucketProvider : INamedBucketProvider
    {
        private readonly IBucketProvider _bucketProvider;

        public string BucketName { get; }

        // ReSharper disable once PublicConstructorInAbstractClass
        public NamedBucketProvider(IBucketProvider bucketProvider, string bucketName)
        {
            _bucketProvider = bucketProvider ?? throw new ArgumentNullException(nameof(bucketProvider));
            BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        }

        public ValueTask<IBucket> GetBucketAsync()
        {
            return _bucketProvider.GetBucketAsync(BucketName);
        }
    }
}
