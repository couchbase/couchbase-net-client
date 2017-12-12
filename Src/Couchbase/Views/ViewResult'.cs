using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Couchbase.Views
{
    /// <summary>
    /// Represents the results of a View query.
    /// </summary>
    /// <typeparam name="T">The Type parameter to be used for deserialization by the <see cref="IDataMapper"/>
    /// implementation.</typeparam>
    public class ViewResult<T> : IViewResult<T>
    {
        public ViewResult()
        {
            Rows = new List<ViewRow<T>>();
            Error = string.Empty;
            Message = string.Empty;
        }

        /// <summary>
        /// The total number of rows.
        /// </summary>
        public virtual uint TotalRows { get; internal set; }

        /// <summary>
        /// The results of the query if successful as a <see cref="IEnumerable{T}"/>
        /// </summary>
        public virtual IEnumerable<ViewRow<T>> Rows { get; internal set; }

        /// <summary>
        /// Returns the value of each element within the <see cref="Rows"/> property as a <see cref="IEnumerable{T}"/>.
        /// </summary>
        public virtual IEnumerable<T> Values
        {
            get { return Rows.Select(x => x.Value); }
        }

        /// <summary>
        /// An error message if one occured.
        /// </summary>
        public string Error { get; internal set; }

        /// <summary>
        /// The HTTP Status Code for the request
        /// </summary>
        public HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// True if the request was successful
        /// </summary>
        public bool Success { get; internal set; }

        /// <summary>
        /// An optional message returned by the server or the client
        /// </summary>
        public string Message { get; internal set; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Checks to see if the operation is eligible for a retry.
        /// </summary>
        /// <returns>True if the operation should not be retried.</returns>
        [Obsolete("Please use IResult.ShouldRetry() instead.")]
        public bool CannotRetry()
        {
            return !ShouldRetry();
        }

        /// <summary>
        /// If the response indicates the request is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>Intended for internal use only.</remarks>
        public bool ShouldRetry()
        {
            if (Success)
            {
                return false;
            }

            // View status code retry strategy
            // https://docs.google.com/document/d/1GhRxvPb7xakLL4g00FUi6fhZjiDaP33DTJZW7wfSxrI/edit
            switch (StatusCode)
            {
                case HttpStatusCode.MultipleChoices: // 300
                case HttpStatusCode.MovedPermanently: // 301
                case HttpStatusCode.Found: // 302
                case HttpStatusCode.SeeOther: // 303
                case HttpStatusCode.TemporaryRedirect: //307
                case HttpStatusCode.RequestTimeout: // 408
                case HttpStatusCode.Conflict: // 409
                case HttpStatusCode.PreconditionFailed: // 412
                case HttpStatusCode.RequestedRangeNotSatisfiable: // 416
                case HttpStatusCode.ExpectationFailed: // 417
                case HttpStatusCode.BadGateway: // 502
                case HttpStatusCode.ServiceUnavailable: // 503
                case HttpStatusCode.GatewayTimeout: // 504
                    return true;
                case HttpStatusCode.NotFound: // 404
                    return !(Error.Contains("not_found") && (Error.Contains("missing") || Error.Contains("deleted")));
                case HttpStatusCode.InternalServerError: // 500
                    return !(Error.Contains("error") && Error.Contains("{not_found, missing_named_view}"));
                default:
                    return false;
            }
        }
    }

    internal class ViewResultData<T>
    {
        public string error { get; set; }
        public string reason { get; set; }
        public uint total_rows { get; set; }
        public IEnumerable<ViewRowData<T>> rows { get; set; }

        public ViewResultData()
        {
            rows = new List<ViewRowData<T>>();
            error = string.Empty;
            reason = string.Empty;
        }

        internal ViewResult<T> ToViewResult()
        {
            return new ViewResult<T>
            {
                Error = error,
                Message = reason,
                TotalRows = total_rows,
                Rows = rows != null ? rows.Select(r => r.ToViewRow()) : null
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
