using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Query;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <remarks>Volatile</remarks>
    public interface IScope
    {
        string Id { get; }

        string Name { get; }

        /// <summary>
        /// The bucket that owns this scope.
        /// </summary>
        IBucket Bucket { get; }

        [Obsolete("Use asynchronous equivalent instead.")]
        ICouchbaseCollection this[string name] { get; }

        [Obsolete("Use asynchronous equivalent instead.")]
        ICouchbaseCollection Collection(string collectionName);

        Task<ICouchbaseCollection> CollectionAsync(string collectionName);

        /// <summary>
        /// Scope level querying of collections.
        /// </summary>
        /// <typeparam name="T">The record type returned by the query.</typeparam>
        /// <param name="statement">The N1QL statement to be executed.</param>
        /// <param name="options">Any optional parameters to pass with the query.</param>
        /// <returns></returns>
        Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options = default);

        /// <summary>
        /// Scope level analytics querying of collections.
        /// </summary>
        /// <typeparam name="T">The record type returned by the query.</typeparam>
        /// <param name="statement">The N1QL statement to be executed.</param>
        /// <param name="options">Any optional parameters to pass with the query.</param>
        /// <returns></returns>
        Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = default);
    }
}
