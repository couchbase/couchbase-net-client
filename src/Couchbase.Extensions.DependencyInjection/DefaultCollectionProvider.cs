namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Base implementation for <see cref="INamedCollectionProvider"/> for a default collection.
    /// </summary>
    /// <remarks>
    /// Used to simplify creating AOT-compatible instances of <see cref="INamedCollectionProvider"/>.
    /// </remarks>
    public abstract class DefaultCollectionProvider : NamedCollectionProvider
    {
        /// <summary>
        /// Constructs a new DefaultCollectionProvider.
        /// </summary>
        /// <param name="bucketProvider">The <see cref="IBucketProvider"/> for the bucket containing this collection.</param>
        protected DefaultCollectionProvider(INamedBucketProvider bucketProvider)
            : base(bucketProvider, DefaultScopeName, DefaultCollectionName)
        {
        }
    }
}
