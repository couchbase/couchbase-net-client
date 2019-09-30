namespace Couchbase.Query
{
    /// <summary>
    /// Provides a method signature for invalidating and clearing a cache.
    /// </summary>
    public interface IQueryCacheInvalidator
    {
        /// <summary>
        /// Invalidates and clears the query cache. This method can be used to explicitly clear the internal N1QL query cache. This cache will
        /// be filled with non-adhoc query statements (query plans) to speed up those subsequent executions. Triggering this method will wipe
        /// out the complete cache, which will not cause an interruption but rather all queries need to be re-prepared internally. This method
        /// is likely to be deprecated in the future once the server side query engine distributes its state throughout the cluster.
        /// </summary>
        /// <returns>An <see cref="int"/> representing the size of the cache before it was cleared.</returns>
        int InvalidateQueryCache();
    }
}
