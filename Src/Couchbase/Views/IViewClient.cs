using System.Net.Http;
using System.Threading.Tasks;

namespace Couchbase.Views
{
    /// <summary>
    /// An interface for client-side support for querying Couchbase views.
    /// </summary>
    public interface IViewClient
    {
        /// <summary>
        /// Executes a <see cref="IViewQuery"/> asynchronously against a View.
        /// </summary>
        /// <typeparam name="T">The Type parameter of the result returned by the query.</typeparam>
        /// <param name="query">The <see cref="IViewQueryable"/> to execute on.</param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        Task<IViewResult<T>> ExecuteAsync<T>(IViewQueryable query);

        /// <summary>
        /// Executes a <see cref="IViewQuery"/> synchronously against a View.
        /// </summary>
        /// <typeparam name="T">The Type parameter of the result returned by the query.</typeparam>
        /// <param name="query">The <see cref="IViewQueryable"/> to execute on.</param>
        /// <returns>The <see cref="IViewResult{T}"/> instance which is the results of the query.</returns>
        IViewResult<T> Execute<T>(IViewQueryable query);

        /// <summary>
        /// An <see cref="IDataMapper"/> instance for handling deserialization of <see cref="IViewResult{T}"/>
        /// and mapping then to the queries Type paramater.
        /// </summary>
        IDataMapper Mapper { get; }

        /// <summary>
        /// The <see cref="HttpClient"/> used to execute the HTTP request against the Couchbase server.
        /// </summary>
        HttpClient HttpClient { get; }
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
