using System;
using System.Threading.Tasks;
using Couchbase.KeyValue;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    /// <summary>
    /// Base implementation for <see cref="INamedCollectionProvider"/>, used as the base
    /// class by <see cref="NamedCollectionProxyGenerator"/>.
    /// </summary>
    internal abstract class NamedCollectionProvider : INamedCollectionProvider
    {
        private readonly INamedBucketProvider _bucketProvider;
        private ICouchbaseCollection? _collection;

        /// <inheritdoc />
        public string ScopeName { get; }

        /// <inheritdoc />
        public string CollectionName { get; }

        // ReSharper disable once PublicConstructorInAbstractClass
        public NamedCollectionProvider(INamedBucketProvider bucketProvider, string scopeName, string collectionName)
        {
            _bucketProvider = bucketProvider ?? throw new ArgumentNullException(nameof(bucketProvider));
            ScopeName = scopeName ?? throw new ArgumentNullException(nameof(scopeName));
            CollectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
        }

        public async ValueTask<ICouchbaseCollection> GetCollectionAsync()
        {
            // Note: If GetCollectionAsync is called in parallel by two different callers, the cache may miss
            // on both and result in two calls to GetBucketAsync. However, this local cache is just an optimization,
            // GetBucketAsync is thread safe and will return the same IBucket to each caller. So this is safe to
            // do lock-free.

            if (_collection != null)
            {
                return _collection;
            }

            var bucket = await _bucketProvider.GetBucketAsync().ConfigureAwait(false);

            // The ScopeAsync and CollectionAsync methods just forward to the synchronous methods,
            // so we can optimize by using the sync overloads.

            // ReSharper disable MethodHasAsyncOverload
            return _collection = bucket.Scope(ScopeName).Collection(CollectionName);
            // ReSharper restore MethodHasAsyncOverload
        }
    }
}
