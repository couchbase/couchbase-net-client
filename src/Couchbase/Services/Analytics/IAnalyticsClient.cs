using System;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Services.Analytics
{
    internal interface IAnalyticsClient
    {
        /// <summary>
        /// Gets the timestamp of the last activity.
        /// </summary>
        DateTime? LastActivity { get; }

        /// <summary>
        /// Executes an Analytics request against a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type to cast the resulting rows to.</typeparam>
        /// <param name="request">The <see cref="IAnalyticsRequest"/> to execute.</param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        IAnalyticsResult<T> Query<T>(IAnalyticsRequest request);

        /// <summary>
        /// Asynchronously executes an analytics request against a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type to cast the resulting rows to.</typeparam>
        /// <param name="request">The <see cref="IAnalyticsRequest"/> to execute.</param>
        /// <param name="token">A cancellation token that can be used to cancel the request.</param>
        /// <returns>A <see cref="Task{T}"/> that can be awaited on for the results.</returns>
        Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest request, CancellationToken token);
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
