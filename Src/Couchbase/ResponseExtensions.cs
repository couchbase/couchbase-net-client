using System;
using Couchbase.Core;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Views;

namespace Couchbase
{
    /// <summary>
    /// Extensions for Couchbase response objects.
    /// </summary>
    public static class ResponseExtensions
    {
        /// <summary>
        /// Throws a <see cref="CouchbaseKeyValueResponseException"/> on any error.
        /// </summary>
        /// <param name="result">The result.</param>
        public static void EnsureSuccess(this IOperationResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            if (!result.Success)
            {
                throw CouchbaseKeyValueResponseException.FromResult(result);
            }
        }

        /// <summary>
        /// Throws a <see cref="CouchbaseKeyValueResponseException"/> on any error.
        /// </summary>
        /// <param name="result">The result.</param>
        public static void EnsureSuccess(this IDocumentResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            if (!result.Success)
            {
                throw CouchbaseKeyValueResponseException.FromResult(result);
            }
        }

        /// <summary>
        /// Throws a <see cref="CouchbaseViewResponseException"/> on any error.
        /// </summary>
        /// <typeparam name="T">View result row type.</typeparam>
        /// <param name="result">The result.</param>
        public static void EnsureSuccess<T>(this IViewResult<T> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            if (!result.Success)
            {
                throw CouchbaseViewResponseException.FromResult(result);
            }
        }

        /// <summary>
        /// Throws a <see cref="CouchbaseQueryResponseException"/> on any error.
        /// </summary>
        /// <typeparam name="T">Query result row type.</typeparam>
        /// <param name="result">The result.</param>
        public static void EnsureSuccess<T>(this IQueryResult<T> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            if (!result.Success)
            {
                throw CouchbaseQueryResponseException.FromResult(result);
            }
        }

        /// <summary>
        /// Throws a <see cref="CouchbaseSearchResponseException"/> on any error.
        /// </summary>
        /// <param name="result">The result.</param>
        public static void EnsureSuccess(this ISearchQueryResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            if (!result.Success)
            {
                throw CouchbaseSearchResponseException.FromResult(result);
            }
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
