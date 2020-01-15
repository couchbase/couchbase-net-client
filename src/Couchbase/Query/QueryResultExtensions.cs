using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Query
{
    public static class QueryResultExtensions
    {
        private static readonly List<QueryStatus> StaleStatuses = new List<QueryStatus>
        {
            QueryStatus.Fatal,
            QueryStatus.Timeout,
            QueryStatus.Errors
        };

        private static readonly List<int> StaleErrorCodes = new List<int>
        {
            (int) ErrorPrepared.PreparedStatementNotFound,
            (int) ErrorPrepared.Unrecognized,
            (int) ErrorPrepared.UnableToDecode,
            (int) ErrorPrepared.Generic,
            (int) ErrorPrepared.IndexNotFound
        };

        /// <summary>
        /// Determines whether a prepared query's plan stale.
        /// </summary>
        /// <param name="queryResult">The N1QL query result.</param>
        /// <returns>
        ///   <c>true</c> if query plan is stale; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsQueryPlanStale<T>(this IQueryResult<T> queryResult)
        {
            return StaleStatuses.Contains(queryResult.MetaData.Status) &&
                   queryResult.Errors.Any(error => StaleErrorCodes.Contains(error.Code));
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
