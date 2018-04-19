using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Couchbase.Utils;

namespace Couchbase.Search
{
    /// <summary>
    /// Exception thrown for a failed search query response if EnsureSucess is called.
    /// </summary>
    public class CouchbaseSearchResponseException : CouchbaseResponseException
    {
        /// <summary>
        /// <see cref="SearchStatus"/> returned from Couchbase.
        /// </summary>
        public SearchStatus Status { get; private set; }

        /// <summary>
        /// Errors returned from Couchbase.
        /// </summary>
        public IReadOnlyCollection<string> Errors { get; private set; }

        /// <summary>
        /// Creates a new CouchbaseSearchResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="SearchStatus"/> returned from Couchbase.</param>
        /// <param name="errors">Errors returned from Couchbase.</param>
        public CouchbaseSearchResponseException(string message, SearchStatus status, IList<string> errors) :
            this(message, status, errors, null)
        {
        }

        /// <summary>
        /// Creates a new CouchbaseSearchResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="SearchStatus"/> returned from Couchbase.</param>
        /// <param name="errors">Errors returned from Couchbase.</param>
        /// <param name="innerException">Exception included in the response from Couchbase.</param>
        public CouchbaseSearchResponseException(string message, SearchStatus status, IList<string> errors,
            Exception innerException) :
            base(message, innerException)
        {
            Status = status;
            Errors = new ReadOnlyCollection<string>(errors ?? new string[] {});
        }

        /// <summary>
        /// Creates a new CouchbaseQueryResponseException.
        /// </summary>
        /// <param name="result">Result from Couchbase</param>
        internal static CouchbaseSearchResponseException FromResult(ISearchQueryResult result)
        {
            return new CouchbaseSearchResponseException(
                ExceptionUtil.GetResponseExceptionMessage(result.Message, result.Status),
                result.Status, result.Errors, result.Exception);
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
