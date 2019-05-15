using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace Couchbase.Services.Query
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
        public Guid RequestId { get; internal set; }

        /// <summary>
        /// Gets the clientContextID of the request, if one was supplied. Used for debugging.
        /// </summary>
        /// <value>
        /// The client context identifier.
        /// </value>
        public string ClientContextId { get; internal set; }

        /// <summary>
        /// Gets the schema of the results. Present only when the query completes successfully.
        /// </summary>
        /// <value>
        /// The signature of the schema of the request.
        /// </value>
        public dynamic Signature { get; internal set; }

        /// <summary>
        /// Gets a list of all the objects returned by the query. An object can be any JSON value.
        /// </summary>
        /// <value>
        /// A a list of all the objects returned by the query.
        /// </value>
        public List<T> Rows { get; internal set; }

        /// <summary>
        /// Gets the status of the request; possible values are: success, running, errors, completed, stopped, timeout, fatal.
        /// </summary>
        /// <value>
        /// The status of the request.
        /// </value>
        public QueryStatus Status { get; internal set; }

        /// <summary>
        /// Gets a list of 0 or more error objects; if an error occurred during processing of the request, it will be represented by an error object in this list.
        /// </summary>
        /// <value>
        /// The errors.
        /// </value>
        public List<Error> Errors { get; internal set; }

        /// <summary>
        /// Gets a list of 0 or more warning objects; if a warning occurred during processing of the request, it will be represented by a warning object in this list.
        /// </summary>
        /// <value>
        /// The warnings.
        /// </value>
        public List<Warning> Warnings { get; internal set; }

        /// <summary>
        /// Gets an object containing metrics about the request.
        /// </summary>
        /// <value>
        /// The metrics.
        /// </value>
        public Metrics Metrics { get; internal set; }

        /// <summary>
        /// Gets the requet N1QL query profile.
        /// </summary>
        /// <value>
        /// The profile.
        /// </value>
        public dynamic Profile { get; internal set; }

        /// <summary>
        /// Gets the HTTP status code.
        /// </summary>
        /// <value>
        /// The HTTP status code.
        /// </value>
        [IgnoreDataMember]
        public HttpStatusCode HttpStatusCode { get; internal set; }

        /// <summary>
        /// If the response is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>Intended for internal use only.</remarks>
        public bool ShouldRetry()
        {
            switch (Status)
            {
                case QueryStatus.Errors:
                case QueryStatus.Timeout:
                case QueryStatus.Fatal:
                    return Errors.Any(error =>
                        error.Code == (int) ErrorPrepared.Unrecognized ||
                        error.Code == (int) ErrorPrepared.UnableToDecode ||
                        error.Code == (int) ErrorPrepared.IndexNotFound ||
                        (error.Code == (int) ErrorPrepared.Generic && error.Message != null && error.Message.Contains(QueryClient.Error5000MsgQueryPortIndexNotFound))
                    );
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the N1QL rows from the result array.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the N1QL rows.
        /// </returns>
        public IEnumerator<T> GetEnumerator()
        {
            return Rows.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the N1QL rows from the result array.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the N1QL rows.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("ClientContextId: {0}", ClientContextId);
            sb.AppendFormat("Message: {0}", Message);
            if (Errors != null)
            {
                foreach (var error in Errors)
                {
                    sb.AppendFormat("Error: {0} {1}", error.Code, error.Message);
                }
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            //noop
        }
    }

    internal class QueryResultData<T>
    {
        public Guid requestID { get; set; }
        public string clientContextID { get; set; }
        public dynamic signature { get; set; }
        public IEnumerable<T> results { get; set; }
        public QueryStatus status { get; set; }
        public IEnumerable<ErrorData> errors { get; set; }
        public IEnumerable<WarningData> warnings { get; set; }
        public MetricsData metrics { get; set; }
        public dynamic profile { get; set; }

        public QueryResultData()
        {
            results = new List<T>();
            errors = new List<ErrorData>();
            warnings = new List<WarningData>();
            metrics = new MetricsData();
        }

        internal QueryResult<T> ToQueryResult()
        {
            return new QueryResult<T>
            {
                RequestId = requestID,
                ClientContextId = clientContextID,
                Signature = signature,
                Rows = results != null ? results.ToList() : new List<T>(0),
                Status = status,
                Errors = errors != null ? errors.Select(e => e.ToError()).ToList() : null,
                Warnings = warnings != null ? warnings.Select(w => w.ToWarning()).ToList() : null,
                Metrics = metrics != null ? metrics.ToMetrics() : null,
                Profile = profile
            };
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
