using System.Threading.Tasks;
using Couchbase.Extensions.DependencyInjection.Internal;
using Couchbase.KeyValue;

namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Base implementation for <see cref="INamedCollectionProvider"/>.
    /// </summary>
    /// <remarks>
    /// Used to simplify creating AOT-compatible instances of <see cref="INamedCollectionProvider"/>.
    /// </remarks>
    public abstract class NamedCollectionProvider : INamedCollectionProvider
    {
        internal const string DefaultScopeName = "_default";
        internal const string DefaultCollectionName = "_default";

        private readonly INamedBucketProvider _bucketProvider;
        private ICouchbaseCollection? _collection;

        /// <inheritdoc />
        public string ScopeName { get; }

        /// <inheritdoc />
        public string CollectionName { get; }

        // ReSharper disable once PublicConstructorInAbstractClass
        public NamedCollectionProvider(INamedBucketProvider bucketProvider, string scopeName, string collectionName)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (bucketProvider is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(bucketProvider));
            }
            if (scopeName is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(scopeName));
            }
            if (collectionName is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(collectionName));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

            _bucketProvider = bucketProvider;
            ScopeName = scopeName;
            CollectionName = collectionName;
        }

        public virtual ValueTask<ICouchbaseCollection> GetCollectionAsync()
        {
            async Task<ICouchbaseCollection> GetCollectionInternalAsync()
            {
                var bucket = await _bucketProvider.GetBucketAsync().ConfigureAwait(false);

                // The ScopeAsync and CollectionAsync methods just forward to the synchronous methods,
                // so we can optimize by using the sync overloads.

                // ReSharper disable MethodHasAsyncOverload
                return _collection = bucket.Scope(ScopeName).Collection(CollectionName);
            }

            // Note: If GetCollectionAsync is called in parallel by two different callers, the cache may miss
            // on both and result in two calls to GetBucketAsync. However, this local cache is just an optimization,
            // GetBucketAsync is thread safe and will return the same IBucket to each caller. So this is safe to
            // do lock-free.

            var collection = _collection;
            return collection is not null
                ? new ValueTask<ICouchbaseCollection>(collection)
                : new ValueTask<ICouchbaseCollection>(GetCollectionInternalAsync());
        }
    }
}
