using System;
using Couchbase.IO;
using Couchbase.Utils;

namespace Couchbase.Core
{
    /// <summary>
    /// Exeception thrown for a failed key/value response if EnsureSucess is called.
    /// </summary>
    public class CouchbaseKeyValueResponseException : CouchbaseResponseException
    {
        /// <summary>
        /// <see cref="ResponseStatus"/> returned from Couchbase.
        /// </summary>
        public ResponseStatus Status { get; private set; }

        /// <summary>
        /// Creates a new CouchbaseKeyValueResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="ResponseStatus"/> returned from Couchbase.</param>
        public CouchbaseKeyValueResponseException(string message, ResponseStatus status) :
            this(message, status, null)
        {
        }

        /// <summary>
        /// Creates a new CouchbaseKeyValueResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="ResponseStatus"/> returned from Couchbase.</param>
        /// <param name="innerException">Exception included in the response from Couchbase.</param>
        public CouchbaseKeyValueResponseException(string message, ResponseStatus status, Exception innerException) :
            base(message, innerException)
        {
            Status = status;
        }

        /// <summary>
        /// Creates a new CouchbaseKeyValueResponseException.
        /// </summary>
        /// <param name="result">Result from Couchbase</param>
        internal static CouchbaseKeyValueResponseException FromResult(IOperationResult result)

        {
            return new CouchbaseKeyValueResponseException(
                ExceptionUtil.GetResponseExceptionMessage(result.Message, result.Status),
                result.Status, result.Exception);
        }

        /// <summary>
        /// Creates a new CouchbaseKeyValueResponseException.
        /// </summary>
        /// <param name="result">Result from Couchbase</param>
        internal static CouchbaseKeyValueResponseException FromResult(IDocumentResult result)

        {
            return new CouchbaseKeyValueResponseException(
                ExceptionUtil.GetResponseExceptionMessage(result.Message, result.Status),
                result.Status, result.Exception);
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
