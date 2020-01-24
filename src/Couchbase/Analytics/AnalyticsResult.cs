using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DataMapping;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Retry;
using Couchbase.Query;

namespace Couchbase.Analytics
{
    internal class AnalyticsResult<T> : IAnalyticsResult<T>
    {
        internal List<T> RowList { get; set; }

        /// <inheritdoc />
        public IAsyncEnumerable<T> Rows => this;

        /// <summary>
        /// Gets the meta data associated with the analytics result.
        /// </summary>
        public AnalyticsMetaData MetaData { get; internal set; }

        internal List<Error> Errors { get; set; }

        /// <inheritdoc />
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            // TODO: Implement actual streaming under the hood
            return RowList.ToAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
        }

        internal HttpStatusCode HttpStatusCode { get; set; }

        /// <summary>
        /// If the response indicates the request is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Intended for internal use only.
        /// </remarks>
        internal bool ShouldRetry()
        {
            SetRetryReasonIfFailed();
            return ((IServiceResult)this).RetryReason != RetryReason.NoRetry;
        }

        internal void SetRetryReasonIfFailed()
        {
            if (HttpStatusCode == HttpStatusCode.OK)
                ((IServiceResult) this).RetryReason = RetryReason.NoRetry;
            else
            {
                foreach (var error in Errors)
                {
                    switch (error.Code)
                    {
                        case 21002:
                            throw new AmbiguousTimeoutException("Analytics query timed out.");
                        case 23000:
                        case 23003:
                        case 23007:
                            ((IServiceResult) this).RetryReason = RetryReason.AnalyticsTemporaryFailure;
                            return;
                        default:
                            throw new CouchbaseException($"Analytics query failed: {error.Code}");
                    }
                }
            }
        }

        RetryReason IServiceResult.RetryReason { get; set; } = RetryReason.NoRetry;
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
                RowList = results.ToList(),
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
