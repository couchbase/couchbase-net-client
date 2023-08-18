using System;
using System.Threading.Tasks;

namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Base implementation for <see cref="INamedBucketProvider"/>.
    /// </summary>
    /// <remarks>
    /// Used to simplify creating AOT-compatible instances of <see cref="INamedBucketProvider"/>.
    /// </remarks>
    public abstract class NamedBucketProvider : INamedBucketProvider
    {
        private readonly IBucketProvider _bucketProvider;

        public string BucketName { get; }

        // ReSharper disable once PublicConstructorInAbstractClass
        public NamedBucketProvider(IBucketProvider bucketProvider, string bucketName)
        {
            _bucketProvider = bucketProvider ?? throw new ArgumentNullException(nameof(bucketProvider));
            BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        }

        public virtual ValueTask<IBucket> GetBucketAsync()
        {
            return _bucketProvider.GetBucketAsync(BucketName);
        }
    }
}
