using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Authentication.SASL;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Views;

namespace Couchbase.Core
{
    /// <summary>
    /// Represents a Couchbase Server node on the network.
    /// </summary>
    public interface IServer : IDisposable, IQueryCacheInvalidator
    {
        /// <summary>
        /// Gets a value indicating whether this instance is MGMT node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is MGMT node; otherwise, <c>false</c>.
        /// </value>
        bool IsMgmtNode { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is query node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is query node; otherwise, <c>false</c>.
        /// </value>
        bool IsQueryNode { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is data node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is data node; otherwise, <c>false</c>.
        /// </value>
        bool IsDataNode { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is index node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is index node; otherwise, <c>false</c>.
        /// </value>
        bool IsIndexNode { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is view node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is view node; otherwise, <c>false</c>.
        /// </value>
        bool IsViewNode { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is an FTS node.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is search node; otherwise, <c>false</c>.
        /// </value>
        bool IsSearchNode { get; }

        bool IsSecure { get; }

        void MarkDead();

        bool IsDown { get; }

        IConnectionPool ConnectionPool { get; }

        IViewClient ViewClient { get; }

        IQueryClient QueryClient { get; }

        /// <summary>
        /// Gets the <see cref="ISearchClient"/> for this node if <see cref="IsSearchNode"/> is <c>true</c>.
        /// </summary>
        /// <value>
        /// The search client.
        /// </value>
        ISearchClient SearchClient { get; }

        IPEndPoint EndPoint { get; }

        void CheckOnline(bool isDead);

        /// <summary>
        /// Sends a <see cref="IFtsQuery"/>  request to a Couchbase node that has FTS service configured asynchronously.
        /// </summary>
        /// <returns>A <see cref="Task{ISearchQueryResult}"/> representing the response from the FTS service.</returns>
        Task<ISearchQueryResult> SendAsync(SearchQuery searchQuery);

        /// <summary>
        /// Sends a <see cref="IFtsQuery"/>  request to a Couchbase node that has FTS service configured asynchronously.
        /// </summary>
        /// <returns>A <see cref="ISearchQueryResult"/> representing the response from the FTS service.</returns>
        ISearchQueryResult Send(SearchQuery searchQuery);

        /// <summary>
        /// Sends a key/value operation that contains no body to it's mapped server asynchronously.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation"/> to send.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task SendAsync(IOperation operation);

        /// <summary>
        /// Sends a key/value operation to it's mapped server asynchronously.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> T of the body.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}"/> to send.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task SendAsync<T>(IOperation<T> operation);

        /// <summary>
        /// Sends a key/value operation that contains a body to it's mapped server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> T of the body.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}"/> to send.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> representing the result of the operation.</returns>
        IOperationResult<T> Send<T>(IOperation<T> operation);

        /// <summary>
        /// Sends a key/value operation that contains no body to it's mapped server.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation"/> to send.</param>
        /// <returns>An <see cref="IOperationResult"/> representing the result of the operation.</returns>
        IOperationResult Send(IOperation operation);

        /// <summary>
        /// Sends a request for a View to the server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> T of the body for each row result.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> representing the query.</param>
        /// <returns>An <see cref="IViewResult{T}"/> representing the result of the query.</returns>
        IViewResult<T> Send<T>(IViewQueryable query);

        /// <summary>
        /// Sends a request for a View to the server asynchronously.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> T of the body for each row result.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> representing the query.</param>
        /// <returns>An <see cref="Task{IViewResult}"/> object representing the asynchronous operation.</returns>
        Task<IViewResult<T>> SendAsync<T>(IViewQueryable query);

        /// <summary>
        /// Sends a request for a N1QL query to the server asynchronously.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> T of the body for each row (or document) result.</typeparam>
        /// <param name="queryRequest">A <see cref="IQueryRequest"/> object.</param>
        /// <returns>An <see cref="Task{IQueryResult}"/> object representing the asynchronous operation.</returns>
        IQueryResult<T> Send<T>(IQueryRequest queryRequest);

        /// <summary>
        /// Sends a request for a N1QL query to the server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> T of the body for each row (or document) result.</typeparam>
        /// <param name="queryRequest">A <see cref="IQueryRequest"/> object.</param>
        Task<IQueryResult<T>> SendAsync<T>(IQueryRequest queryRequest);

        /// <summary>
        /// Sends a request for a N1QL query to the server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> T of the body for each row (or document) result.</typeparam>
        /// <param name="queryRequest">A <see cref="IQueryRequest"/> object.</param>
        /// <param name="cancellationToken">Token which can cancel the query.</param>
        Task<IQueryResult<T>> SendAsync<T>(IQueryRequest queryRequest, CancellationToken cancellationToken);

        /// <summary>
        /// Sends an analytics request to the server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> T of the body for each row (or document) result.</typeparam>
        /// <param name="analyticsRequest">The analytics request.</param>
        /// <returns></returns>
        IAnalyticsResult<T> Send<T>(IAnalyticsRequest analyticsRequest);

        /// <summary>
        /// Asynchronously sends an analytics request to the server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type" /> T of the body for each row (or document) result.</typeparam>
        /// <param name="analyticsRequest">The analytics request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        Task<IAnalyticsResult<T>> SendAsync<T>(IAnalyticsRequest analyticsRequest, CancellationToken cancellationToken);

        Uri CachedViewBaseUri { get; }
        Uri CachedQueryBaseUri { get; }

        /// <summary>
        /// Gets the clustermap rev# of the <see cref="Server"/>.
        /// </summary>
        /// <value>
        /// The revision.
        /// </value>
        uint Revision { get; }

        bool IsAnalyticsNode { get; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion