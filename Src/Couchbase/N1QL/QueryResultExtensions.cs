using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.N1QL
{
    internal static class QueryResultExtensions
    {
        /// <summary>
        /// Determines whether the query plan is stale and should be purged from the cache and retried.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queryResult">The query result.</param>
        /// <returns>
        ///   <c>true</c> if the query plan is stale; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsQueryPlanStale<T>(this IQueryResult<T> queryResult)
        {
            /* Stale plan retry logic:
               4040 – Named Prepared Statement Not Found
               4050 - Could not translate the request parameter to a prepared statement
               4070 – Could not translate the request parameter to a prepared statement
               5000 – Generic for many error conditions*/

            return queryResult.ShouldRetry() &&
                (queryResult.Status == QueryStatus.Fatal || queryResult.Status == QueryStatus.Timeout) &&
                   queryResult.Errors.Any(x => x.Code == 4040 || x.Code == 4050 || x.Code == 4070 || x.Code == 5000);
        }
    }
}
