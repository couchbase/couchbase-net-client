using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.N1QL;

namespace Couchbase.Analytics
{
    public interface IAnalyticsDeferredResultHandle<T>
    {
        /// <summary>
        /// Gets the current status of the deferred query.
        /// NOTE: This is an experimental API and may change in the future.
        /// </summary>
        /// <returns>The current <see cref="QueryStatus"/> for the deferred query.</returns>
        QueryStatus GetStatus();

        /// <summary>
        /// Gets the current status of the deferred query asynchronously.
        /// NOTE: This is an experimental API and may change in the future.
        /// </summary>
        /// <returns>The current <see cref="QueryStatus"/> for the deferred query.</returns>
        Task<QueryStatus> GetStatusAsync();

        /// <summary>
        /// Gets the query result for a deferred query.
        /// NOTE: This is an experimental API and may change in the future.
        /// </summary>
        /// <returns>The query results as a <see cref="IEnumerable{T}"/>.</returns>
        IEnumerable<T> GetRows();

        /// <summary>
        /// Gets the query result for a deferred query asynchronously.
        /// NOTE: This is an experimental API and may change in the future.
        /// </summary>
        /// <returns>The query results as a <see cref="IEnumerable{T}"/>.</returns>
        Task<IEnumerable<T>> GetRowsAsync();
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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
