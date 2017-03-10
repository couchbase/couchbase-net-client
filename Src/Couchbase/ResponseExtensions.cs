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
