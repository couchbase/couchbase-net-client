using System;
using System.Collections.Generic;
using System.Net;
using Couchbase.N1QL;
using Newtonsoft.Json;

namespace Couchbase.N1QL
{
    /// <summary>
    /// The result of a N1QL query.
    /// </summary>
    /// <typeparam name="T">The Type of each row returned.</typeparam>
    /// <remarks>The dynamic keyword works well for the Type T.</remarks>
    public class QueryResult<T> : IQueryResult<T>
    {
        public QueryResult()
        {
           Rows = new List<T>();
           Errors = new List<Error>();
           Warnings = new List<Warning>();
        }

        /// <summary>
        /// True if query was successful.
        /// </summary>
        public bool Success { get; internal set; }

        /// <summary>
        /// Optional message returned by query engine or client
        /// </summary>
        public string Message { get; internal set; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public Exception Exception { get; set; }

        [JsonProperty("request_id")]
        public Guid RequestId { get; internal set; }

        [JsonProperty("client_context_id")]
        public string ClientContextId { get; internal set; }

        [JsonProperty("signature")]
        public dynamic Signature { get; internal set; }

        [JsonProperty("results")]
        public List<T> Rows { get; internal set; }

        [JsonProperty("status")]
        public QueryStatus Status { get; internal set; }

        [JsonProperty("errors")]
        public List<Error> Errors { get; internal set; }

        [JsonProperty("warnings")]
        public List<Warning> Warnings { get; internal set; }

        [JsonProperty("metrics")]
        public Metrics Metrics { get; internal set; }

        [JsonIgnore]
        public HttpStatusCode HttpStatusCode { get; internal set; }

        bool IResult.ShouldRetry()
        {
            var retry = false;
            switch (Status)
            {
                case QueryStatus.Success:
                case QueryStatus.Errors:
                case QueryStatus.Running:
                case QueryStatus.Completed:
                case QueryStatus.Stopped:
                    break;
                case QueryStatus.Timeout:
                case QueryStatus.Fatal:
                    var status = (int) HttpStatusCode;
                    if(status > 399 && status < 500)
                    {
                        break;
                    }
                    retry = true;
                    break;
            }
            return retry;
        }
    }
}
#region [ License information ]

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