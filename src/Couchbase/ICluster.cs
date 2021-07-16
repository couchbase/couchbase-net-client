using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.IO.Serializers;
using Couchbase.Diagnostics;
using Couchbase.Management.Analytics;
using Couchbase.Management.Buckets;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Management.Users;
using Couchbase.Query;
using Couchbase.Search;

#nullable enable

namespace Couchbase
{
    public interface ICluster : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// An <see cref="IServiceProvider"/> which provides access to cluster services, such as <see cref="ITypeSerializer"/>.
        /// </summary>
        IServiceProvider ClusterServices { get; }

        ValueTask<IBucket> BucketAsync(string name);

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

        /// <summary>
        /// Creates diagnostic report that can be used to determine the healthfulness of the cluster. It does not proactively perform any I/O against the network.
        /// </summary>
        /// <param name="options">Optional arguments.</param>
        /// <returns></returns>
        Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions? options = null);

        #endregion

        #region Query

        Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options = default);

        Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = default);

        Task<ISearchResult> SearchQueryAsync(string indexName, ISearchQuery query, SearchOptions? options = default);

        #endregion

        #region Management
        IQueryIndexManager QueryIndexes { get; }

        IAnalyticsIndexManager AnalyticsIndexes { get; }

        ISearchIndexManager SearchIndexes { get; }

        IBucketManager Buckets { get; }

        IUserManager Users { get; }
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
