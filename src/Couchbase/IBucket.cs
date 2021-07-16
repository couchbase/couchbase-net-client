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

        IScope Scope(string scopeName);

        /// <summary>
        /// Gets a scope from  the bucket by name.
        /// </summary>
        /// <param name="scopeName">The name of the scope to fetch.</param>
        /// <returns>A scope that belongs to the bucket.</returns>
        ValueTask<IScope> ScopeAsync(string scopeName);

        IScope DefaultScope();

        /// <summary>
        /// Gets the default scope for the bucket.
        /// </summary>
        /// <returns>The default scope.</returns>
        ValueTask<IScope> DefaultScopeAsync();

        ICouchbaseCollection DefaultCollection();

        /// <summary>
        /// Gets the default collection for the bucket.
        /// </summary>
        /// <returns>The default collection.</returns>
        ValueTask<ICouchbaseCollection> DefaultCollectionAsync();

        ICouchbaseCollection Collection(string collectionName);

        /// <summary>
        /// Gets a collection from the default scope of the bucket by name.
        /// </summary>
        /// <param name="collectionName">The name of the collection to fetch.</param>
        /// <returns>A collection that belongs to the default scope of the bucket.</returns>
        ValueTask<ICouchbaseCollection> CollectionAsync(string collectionName);

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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
