using System;
using System.Threading.Tasks;
using Couchbase.Diagnostics;
using Couchbase.Views;

#nullable enable

namespace Couchbase
{
    public static class BucketExtensions
    {
        /// <summary>
        /// Execute a view query.
        /// </summary>
        /// <typeparam name="TKey">Type of the key for each result row.</typeparam>
        /// <typeparam name="TValue">Type of the value for each result row.</typeparam>
        /// <param name="bucket"><seealso cref="IBucket"/> to execute the query against.</param>
        /// <param name="designDocument">Design document name.</param>
        /// <param name="viewName">View name.</param>
        /// <returns>An <seealso cref="IViewResult{TKey,TValue}"/>.</returns>
        [Obsolete("The View service has been deprecated use the Query service instead.")]
        public static Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(this IBucket bucket, string designDocument, string viewName)
        {
            return bucket.ViewQueryAsync<TKey, TValue>(designDocument, viewName, ViewOptions.Default);
        }

        /// <summary>
        /// Execute a view query.
        /// </summary>
        /// <typeparam name="TKey">Type of the key for each result row.</typeparam>
        /// <typeparam name="TValue">Type of the value for each result row.</typeparam>
        /// <param name="bucket"><seealso cref="IBucket"/> to execute the query against.</param>
        /// <param name="designDocument">Design document name.</param>
        /// <param name="viewName">View name.</param>
        /// <param name="configureOptions">Action to configure the <seealso cref="ViewOptions"/> controlling query execution.</param>
        /// <returns>An <seealso cref="IViewResult{TKey,TValue}"/>.</returns>
        [Obsolete("The View service has been deprecated use the Query service instead.")]
        public static Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(this IBucket bucket, string designDocument,
            string viewName, Action<ViewOptions> configureOptions)
        {
            var options = new ViewOptions();
            configureOptions(options);

            return bucket.ViewQueryAsync<TKey, TValue>(designDocument, viewName, options);
        }

        #region PingAsync

        public static Task<IPingReport> PingAsync(this IBucket bucket, params ServiceType[] serviceTypes)
        {
            return PingAsync(bucket, Guid.NewGuid().ToString(), serviceTypes);
        }

        public static Task<IPingReport> PingAsync(this IBucket bucket, Action<PingOptions> configureOptions)
        {
            var options = new PingOptions();
            configureOptions(options);

            return bucket.PingAsync(options);
        }

        public static Task<IPingReport> PingAsync(this IBucket bucket, string reportId, params ServiceType[] serviceTypes)
        {
            var serviceTypesValue =
                serviceTypes == null || serviceTypes.Length == 0 ? PingOptions.DefaultServiceTypeValues : serviceTypes;
            return bucket.PingAsync(new PingOptions {ReportIdValue = reportId, ServiceTypesValue = serviceTypesValue});
        }

        #endregion

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
