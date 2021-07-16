using System;

#nullable enable

namespace Couchbase.Core
{
    /// <summary>
    /// Provides URIs to reach various Couchbase services.
    /// </summary>
    internal interface IServiceUriProvider
    {
        /// <summary>
        /// Get the base <see cref="Uri"/> for a random node's analytics service.
        /// </summary>
        /// <returns>The base <see cref="Uri"/>.</returns>
        Uri GetRandomAnalyticsUri();

        /// <summary>
        /// Get the base <see cref="Uri"/> for a random node's query service.
        /// </summary>
        /// <returns>The base <see cref="Uri"/>.</returns>
        Uri GetRandomQueryUri();

        /// <summary>
        /// Get the base <see cref="Uri"/> for a random node's search service.
        /// </summary>
        /// <returns>The base <see cref="Uri"/>.</returns>
        Uri GetRandomSearchUri();

        /// <summary>
        /// Get the base <see cref="Uri"/> for a random node's management service.
        /// </summary>
        /// <returns>The base <see cref="Uri"/>.</returns>
        Uri GetRandomManagementUri();

        /// <summary>
        /// Get the base <see cref="Uri"/> for a bucket's view service on a random node.
        /// </summary>
        /// <param name="bucketName">The name of the bucket.</param>
        /// <returns>The base <see cref="Uri"/>.</returns>
        Uri GetRandomViewsUri(string bucketName);
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
