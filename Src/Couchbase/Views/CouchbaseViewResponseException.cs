using System;
using System.Net;
using Couchbase.Utils;

namespace Couchbase.Views
{
    /// <summary>
    /// Exception thrown for a failed view query response if EnsureSucess is called.
    /// </summary>
    public class CouchbaseViewResponseException : CouchbaseResponseException
    {
        /// <summary>
        /// <see cref="HttpStatusCode"/> returned from Couchbase.
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// Creates a new CouchbaseViewResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="HttpStatusCode"/> returned from Couchbase.</param>
        public CouchbaseViewResponseException(string message, HttpStatusCode status) :
            this(message, status, null)
        {
        }

        /// <summary>
        /// Creates a new CouchbaseViewResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="HttpStatusCode"/> returned from Couchbase.</param>
        /// <param name="innerException">Exception included in the response from Couchbase.</param>
        public CouchbaseViewResponseException(string message, HttpStatusCode status, Exception innerException) :
            base(message, innerException)
        {
            StatusCode = status;
        }

        internal static CouchbaseViewResponseException FromResult<T>(IViewResult<T> result)
        {
            return new CouchbaseViewResponseException(
                ExceptionUtil.GetResponseExceptionMessage(result.Message, result.StatusCode),
                result.StatusCode, result.Exception);
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
