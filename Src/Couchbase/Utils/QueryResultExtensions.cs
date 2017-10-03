using System.Text;
using Couchbase.N1QL;

namespace Couchbase.Utils
{
    /// <summary>
    /// Provides methods which extend the <see cref="IQueryResult{T}"/> interface, without actually modifying the interface.
    /// </summary>
    internal static class QueryResultExtensions
    {
        /// <summary>
        /// Converts the <see cref="IQueryResult{T}.Errors"/> collection to a string representation.
        /// </summary>
        /// <typeparam name="T">The Type of the row body.</typeparam>
        /// <param name="query">The <see cref="IQueryResult{T}"/> reference.</param>
        /// <returns></returns>
        public static string GetErrorsAsString<T>(this IQueryResult<T> query)
        {
            var builder = new StringBuilder();
            foreach (var error in query.Errors)
            {
                builder.AppendFormat("{0} - {1}, ", error.Code, error.Message);
            }
            return builder.ToString();
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
