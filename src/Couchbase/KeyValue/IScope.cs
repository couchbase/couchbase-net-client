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
        string Name { get; }

        /// <summary>
        /// The bucket that owns this scope.
        /// </summary>
        IBucket Bucket { get; }

        /// <summary>
        /// Returns true if this is the default scope.
        /// </summary>
        bool IsDefaultScope { get; }

        ICouchbaseCollection this[string name] { get; }

        ICouchbaseCollection Collection(string collectionName);

        ValueTask<ICouchbaseCollection> CollectionAsync(string collectionName);

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
