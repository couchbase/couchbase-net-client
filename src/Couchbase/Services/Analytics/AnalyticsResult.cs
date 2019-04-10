using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Couchbase.Core.DataMapping;
using Couchbase.Services.Query;

namespace Couchbase.Services.Analytics
{
    internal class AnalyticsResult<T> : IAnalyticsResult<T>
    {
        /// <summary>
        /// Gets a list of all the objects returned by the query. An object can be any JSON value.
        /// </summary>
        /// <value>
        /// A a list of all the objects returned by the query.
        /// </value>
        public List<T> Rows { get; internal set; }

        /// <summary>
        /// Gets the meta data associated with the analytics result.
        /// </summary>
        public MetaData MetaData { get; internal set; }

        /// <summary>
        /// Gets the deferred query handle if requested.
        /// <para>
        /// The handle can be used to retrieve a deferred query status and results.
        /// </para>
        /// </summary>
        public IAnalyticsDeferredResultHandle<T> Handle { get; internal set; }

        /// <summary>
        /// If the response indicates the request is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Intended for internal use only.
        /// </remarks>
        internal bool ShouldRetry()
        {
            switch (MetaData.Status)
            {
                case QueryStatus.Errors:
                case QueryStatus.Timeout:
                case QueryStatus.Fatal:
                    return MetaData.Errors != null && MetaData.Errors.Any(error =>
                                   error.Code == 21002 || // Request timed out and will be cancelled
                                   error.Code == 23000 || // Analytics Service is temporarily unavailable
                                   error.Code == 23003 || // Operation cannot be performed during rebalance
                                   error.Code == 23007    // Job queue is full with [string] jobs
                           );
                default:
                    return false;
            }
        }
    }

    internal class AnalyticsResultData<T>
    {
        public Guid requestID { get; set; }
        public string clientContextID { get; set; }
        public dynamic signature { get; set; }
        public IEnumerable<T> results { get; set; }
        public QueryStatus status { get; set; }
        public IEnumerable<ErrorData> errors { get; set; }
        public IEnumerable<WarningData> warnings { get; set; }
        public MetricsData metrics { get; set; }
        public string handle { get; set; }

        public AnalyticsResultData()
        {
            results = new List<T>();
            errors = new List<ErrorData>();
            warnings = new List<WarningData>();
            metrics = new MetricsData();
        }

        internal AnalyticsResult<T> ToQueryResult(HttpClient client, IDataMapper dataMapper)
        {
            var result = new AnalyticsResult<T>
            {
                Rows = results.ToList(),
                MetaData = new MetaData
                {
                    Status = status,
                    RequestId = requestID,
                    ClientContextId = clientContextID,
                    Signature = signature,
                    Errors = errors?.Select(e => e.ToError()).ToList(),
                    Warnings = warnings?.Select(w => w.ToWarning()).ToList(),
                    Metrics = metrics?.ToMetrics()
                }
            };

            if (!string.IsNullOrWhiteSpace(handle))
            {
                result.Handle = new AnalyticsDeferredResultHandle<T>(result, client, dataMapper, handle);
            }

            return result;
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
