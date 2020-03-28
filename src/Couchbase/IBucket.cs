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

        IScope Scope(string scopeName);

        IScope DefaultScope();

        ICouchbaseCollection DefaultCollection();

        ICouchbaseCollection Collection(string collectionName);

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
