using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace Couchbase.N1QL
{
    /// <summary>
    /// The result of a N1QL query.
    /// </summary>
    /// <typeparam name="T">The Type of each row returned.</typeparam>
    /// <remarks>
    /// The dynamic keyword works well for the Type T.
    /// </remarks>
    public class QueryResult<T> : IQueryResult<T>
    {
        public QueryResult()
        {
           Rows = new List<T>();
           Errors = new List<Error>();
           Warnings = new List<Warning>();
           Metrics = new Metrics();
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

        /// <summary>
        /// Gets the request identifier.
        /// </summary>
        /// <value>
        /// The request identifier.
        /// </value>
        [JsonProperty("request_id")]
        public Guid RequestId { get; internal set; }

        /// <summary>
        /// Gets the clientContextID of the request, if one was supplied. Used for debugging.
        /// </summary>
        /// <value>
        /// The client context identifier.
        /// </value>
        [JsonProperty("client_context_id")]
        public string ClientContextId { get; internal set; }

        /// <summary>
        /// Gets the schema of the results. Present only when the query completes successfully.
        /// </summary>
        /// <value>
        /// The signature of the schema of the request.
        /// </value>
        [JsonProperty("signature")]
        public dynamic Signature { get; internal set; }

        /// <summary>
        /// Gets a list of all the objects returned by the query. An object can be any JSON value.
        /// </summary>
        /// <value>
        /// A a list of all the objects returned by the query.
        /// </value>
        [JsonProperty("results")]
        public List<T> Rows { get; internal set; }

        /// <summary>
        /// Gets the status of the request; possible values are: success, running, errors, completed, stopped, timeout, fatal.
        /// </summary>
        /// <value>
        /// The status of the request.
        /// </value>
        [JsonProperty("status")]
        public QueryStatus Status { get; internal set; }

        /// <summary>
        /// Gets a list of 0 or more error objects; if an error occurred during processing of the request, it will be represented by an error object in this list.
        /// </summary>
        /// <value>
        /// The errors.
        /// </value>
        [JsonProperty("errors")]
        public List<Error> Errors { get; internal set; }

        /// <summary>
        /// Gets a list of 0 or more warning objects; if a warning occurred during processing of the request, it will be represented by a warning object in this list.
        /// </summary>
        /// <value>
        /// The warnings.
        /// </value>
        [JsonProperty("warnings")]
        public List<Warning> Warnings { get; internal set; }

        /// <summary>
        /// Gets an object containing metrics about the request.
        /// </summary>
        /// <value>
        /// The metrics.
        /// </value>
        [JsonProperty("metrics")]
        public Metrics Metrics { get; internal set; }

        /// <summary>
        /// Gets the HTTP status code.
        /// </summary>
        /// <value>
        /// The HTTP status code.
        /// </value>
        [JsonIgnore]
        public HttpStatusCode HttpStatusCode { get; internal set; }

        /// <summary>
        /// If the response is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>Intended for internal use only.</remarks>
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
                    if(status > 399)
                    {
                        break;
                    }
                    retry = true;
                    break;
            }
            return retry;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("ClientContextId: {0}", ClientContextId);
            sb.AppendFormat("Message: {0}", Message);
            foreach (var error in Errors)
            {
                sb.AppendFormat("Error: {0} {1}", error.Code, error.Message);
            }
            return sb.ToString();
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
