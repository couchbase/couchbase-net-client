using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Couchbase.Core.DataMapping;
using Couchbase.Query;

namespace Couchbase.Analytics
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
        public AnalyticsMetaData MetaData { get; internal set; }

        internal List<Error> Errors { get; set; }

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
                case AnalyticsStatus.Errors:
                case AnalyticsStatus.Timeout:
                case AnalyticsStatus.Fatal:
                    return Errors != null && Errors.Any(error =>
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
        public string requestID { get; set; }
        public string clientContextID { get; set; }
        public dynamic signature { get; set; }
        public IEnumerable<T> results { get; set; }
        public AnalyticsStatus status { get; set; }
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
                Errors = errors?.Select(e => e.ToError()).ToList(),
                MetaData = new AnalyticsMetaData
                {
                    Status = status,
                    RequestId = requestID,
                    ClientContextId = clientContextID,
                    Signature = signature,
                    Warnings = warnings?.Select(w => w.ToWarning()).ToList(),
                    Metrics = metrics?.ToMetrics()
                }
            };

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
