using System;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Views
{
    /// <summary>
    /// An interface for client-side support for querying Couchbase views.
    /// </summary>
    [Obsolete("The View service has been deprecated use the Query service instead.")]
    internal interface IViewClient
    {
        /// <summary>
        /// Gets the timestamp of the last activity.
        /// </summary>
        DateTime? LastActivity { get; }

        /// <summary>
        /// Executes a <see cref="IViewQuery"/> asynchronously against a View.
        /// </summary>
        /// <typeparam name="TKey">Type of the key for each result row.</typeparam>
        /// <typeparam name="TValue">Type of the value for each result row.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> to execute.</param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        Task<IViewResult<TKey, TValue>> ExecuteAsync<TKey, TValue>(IViewQuery query);
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
