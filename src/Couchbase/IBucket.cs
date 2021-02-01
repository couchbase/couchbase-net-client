using System;
using System.Threading.Tasks;
using Couchbase.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using PingOptions = Couchbase.Diagnostics.PingOptions;

#nullable enable

namespace Couchbase
{
    public interface IBucket : IDisposable, IAsyncDisposable
    {
        string Name { get; }

        /// <summary>
        /// The cluster that owns this bucket.
        /// </summary>
        ICluster Cluster { get; }

        [Obsolete("Use asynchronous equivalent instead.")]
        IScope Scope(string scopeName);

        /// <summary>
        /// Gets a scope from  the bucket by name.
        /// </summary>
        /// <param name="scopeName">The name of the scope to fetch.</param>
        /// <returns>A scope that belongs to the bucket.</returns>
        Task<IScope> ScopeAsync(string scopeName);

        [Obsolete("Use asynchronous equivalent instead.")]
        IScope DefaultScope();

        /// <summary>
        /// Gets the default scope for the bucket.
        /// </summary>
        /// <returns>The default scope.</returns>
        Task<IScope> DefaultScopeAsync();

        [Obsolete("Use asynchronous equivalent instead.")]
        ICouchbaseCollection DefaultCollection();

        /// <summary>
        /// Gets the default collection for the bucket.
        /// </summary>
        /// <returns>The default collection.</returns>
        Task<ICouchbaseCollection> DefaultCollectionAsync();

        [Obsolete("Use asynchronous equivalent instead.")]
        ICouchbaseCollection Collection(string collectionName);

        /// <summary>
        /// Gets a collection from the default scope of the bucket by name.
        /// </summary>
        /// <param name="collectionName">The name of the collection to fetch.</param>
        /// <returns>A collection that belongs to the default scope of the bucket.</returns>
        Task<ICouchbaseCollection> CollectionAsync(string collectionName);

        /// <summary>
        /// Execute a view query.
        /// </summary>
        /// <typeparam name="TKey">Type of the key for each result row.</typeparam>
        /// <typeparam name="TValue">Type of the value for each result row.</typeparam>
        /// <param name="designDocument">Design document name.</param>
        /// <param name="viewName">View name.</param>
        /// <param name="options"><seealso cref="ViewOptions"/> controlling query execution.</param>
        /// <returns>An <seealso cref="IViewResult{TKey,TValue}"/>.</returns>
        Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName, ViewOptions? options = null);

        IViewIndexManager ViewIndexes { get; }

        ICouchbaseCollectionManager Collections { get; }

        #region Diagnostics

        /// <summary>
        /// Actively performs I/O by application-level pinging services and returning their pinged status.
        /// </summary>
        /// <param name="options">Optional arguments.</param>
        /// <returns></returns>
        Task<IPingReport> PingAsync(PingOptions? options = null);

        /// <summary>
        /// Waits until a desired cluster state by default (“online”) is reached or times out.
        /// </summary>
        /// <param name="timeout">The <see cref="TimeSpan"/> duration to wait before throwing an exception.</param>
        /// <param name="options">Optional arguments.</param>
        /// <returns></returns>
        Task WaitUntilReadyAsync(TimeSpan timeout, WaitUntilReadyOptions? options = null);

        #endregion
    }
}
