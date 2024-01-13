using System.Threading.Tasks;
using Couchbase.Extensions.DependencyInjection.Internal;

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
            // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (bucketProvider is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(bucketProvider));
            }
            if (bucketName is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(bucketName));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

            _bucketProvider = bucketProvider;
            BucketName = bucketName;
        }

        public virtual ValueTask<IBucket> GetBucketAsync()
        {
            return _bucketProvider.GetBucketAsync(BucketName);
        }
    }
}
