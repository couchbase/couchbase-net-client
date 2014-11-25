using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json;

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
        [JsonProperty("total_rows")]
        public uint TotalRows { get; internal set; }

        /// <summary>
        /// The results of the query if successful as a <see cref="IEnumerable{T}"/>
        /// </summary>
        [JsonProperty("rows")]
        public IEnumerable<ViewRow<T>> Rows { get; internal set; }

        /// <summary>
        /// Returns the value of each element within the <see cref="Rows"/> property as a <see cref="IEnumerable{T}"/>.
        /// </summary>
        public IEnumerable<T> Values
        {
            get { return Rows.Select(x => x.Value); }
        }

        /// <summary>
        /// An error message if one occured.
        /// </summary>
        [JsonProperty("error")]
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
        [JsonProperty("reason")]
        public string Message { get; internal set; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public System.Exception Exception { get; set; }

        /// <summary>
        /// Checks to see if the operation is eligible for a retry.
        /// </summary>
        /// <returns>True if the operation should not be retried.</returns>
        public bool CannotRetry()
        {
            var cannotRetry = true;
            if (!Success)
            {
                switch (StatusCode)
                {
                    case HttpStatusCode.OK:
                        break;
                        //300's
                    case HttpStatusCode.MultipleChoices:
                    case HttpStatusCode.MovedPermanently:
                    case HttpStatusCode.Found:
                    case HttpStatusCode.SeeOther:
                    case HttpStatusCode.NotModified:
                    case HttpStatusCode.TemporaryRedirect:
                        cannotRetry = false;
                        break;
                        //400's
                    case HttpStatusCode.NotFound:
                        cannotRetry = Check404ForRetry();
                        break;

                    case HttpStatusCode.RequestTimeout:
                    case HttpStatusCode.Conflict:
                    case HttpStatusCode.PreconditionFailed:
                    case HttpStatusCode.RequestedRangeNotSatisfiable:
                    case HttpStatusCode.ExpectationFailed:
                        cannotRetry = false;
                        break;
                        //500's
                    case HttpStatusCode.InternalServerError:
                        cannotRetry = Check500ForRetry();
                        break;

                    case HttpStatusCode.NotImplemented:
                    case HttpStatusCode.BadGateway:
                    case HttpStatusCode.ServiceUnavailable:
                    case HttpStatusCode.GatewayTimeout:
                        cannotRetry = false;
                        break;
                }
            }
            return cannotRetry;
        }

        /// <summary>
        /// Checks to see if a HTTP 500 can result in a retry operation
        /// </summary>
        /// <remarks>Derived rules: https://docs.google.com/document/d/1GhRxvPb7xakLL4g00FUi6fhZjiDaP33DTJZW7wfSxrI/edit</remarks>
        /// <returns>True if the operation should not be retried</returns>
        private bool Check500ForRetry()
        {
            return Error.Contains("error") && Error.Contains("{not_found, missing_named_view}");
        }

        /// <summary>
        /// Checks to see if a HTTP 400 can result in a retry operation
        /// </summary>
        /// <remarks>Derived rules: https://docs.google.com/document/d/1GhRxvPb7xakLL4g00FUi6fhZjiDaP33DTJZW7wfSxrI/edit</remarks>
        /// <returns>True if the operation should not be retried</returns>
        private bool Check404ForRetry()
        {
            return (Error.Contains("not_found") && Error.Contains("missing")) || Error.Contains("deleted");
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
