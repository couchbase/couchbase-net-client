using System;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Services.Query
{
    /// <summary>
    /// An interface for client-side support for executing N1QL queries against a Couchbase Server.
    /// </summary>
    public interface IQueryClient : IQueryCacheInvalidator
    {
        /// <summary>
        /// Gets the timestamp of the last activity.
        /// </summary>
        DateTime? LastActivity { get; }

        /// <summary>
        /// Prepare an ad-hoc N1QL statement for later execution against a Couchbase Server.
        /// </summary>
        /// <param name="statement"></param>
        /// <param name="toPrepare">The <see cref="IQueryOptions"/> containing a N1QL statement to be prepared.</param>
        /// <returns>A <see cref="IQueryResult{T}"/> containing  the <see cref="QueryPlan"/> representing the reusable
        /// and cachable execution plan for the statement.</returns>
        /// <remarks>Most parameters in the IQueryRequest will be ignored, appart from the Statement and the BaseUri.</remarks>
        IQueryResult<QueryPlan> Prepare(string statement, IQueryOptions toPrepare);

        /// <summary>
        /// Asynchronously executes an a N1QL query request against a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type to cast the resulting rows to.</typeparam>
        /// <param name="options">The <see cref="IQueryOptions"/> to execute.</param>
        /// <param name="statment"></param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        Task<IQueryResult<T>> QueryAsync<T>(string statment, IQueryOptions options);

        /// <summary>
        /// Asynchronously executes an a N1QL query request against a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type to cast the resulting rows to.</typeparam>
        /// <param name="statement"></param>
        /// <param name="options">The <see cref="IQueryOptions"/> to execute.</param>
        /// <param name="cancellationToken">Token which can cancel the query.</param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        Task<IQueryResult<T>> QueryAsync<T>(string statement, IQueryOptions options,
            CancellationToken cancellationToken);
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
