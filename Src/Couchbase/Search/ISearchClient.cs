using System.Threading.Tasks;

namespace Couchbase.Search
{
    /// <summary>
    /// A client for making FTS <see cref="IFtsQuery"/> requests and mapping the responses to <see cref="ISearchQueryResult"/>'s.
    /// </summary>
    public interface ISearchClient
    {
        /// <summary>
        /// Executes a <see cref="IFtsQuery"/> request including any <see cref="ISearchParams"/> parameters.
        /// </summary>
        /// <returns></returns>
        ISearchQueryResult Query(SearchQuery searchQuery);

        /// <summary>
        /// Executes a <see cref="IFtsQuery"/> request including any <see cref="ISearchParams"/> parameters asynchronously.
        /// </summary>
        /// <returns></returns>
        Task<ISearchQueryResult> QueryAsync(SearchQuery searchQuery);
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
