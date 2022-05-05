using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Retry;

#nullable enable

namespace Couchbase.Query
{
    /// <summary>
    /// Abstract base class for with shared implementations of <see cref="IQueryResult{T}"/>.
    /// </summary>
    /// <typeparam name="T">The Type of each row returned.</typeparam>
    internal abstract class QueryResultBase<T> : IQueryResult<T>, IServiceResultExceptionInfo
    {
        /// <summary>
        /// Creates a new QueryResultBase.
        /// </summary>
        /// <param name="responseStream"><see cref="Stream"/> to read.</param>
        protected QueryResultBase(Stream responseStream)
        {
            ResponseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
        }

        /// <inheritdoc />
        public IAsyncEnumerable<T> Rows => this;

        /// <summary>
        /// Response stream being deserialized.
        /// </summary>
        protected Stream ResponseStream { get; }

        /// <inheritdoc />
        public QueryMetaData? MetaData { get; protected set; }

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public HttpStatusCode HttpStatusCode { get; set; }

        /// <inheritdoc />
        public List<Error> Errors { get; } = new List<Error>();

        /// <summary>
        /// Returns true if the operation was successful.
        /// </summary>
        /// <remarks>
        /// If Success is false, use the Message property to help determine the reason.
        /// </remarks>
        public bool Success { get; internal set; }

        /// <summary>
        /// If the operation wasn't successful, a message indicating why it was not successful.
        /// </summary>
        public string? Message { get; internal set; }

        /// <summary>
        /// If the response indicates the request is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Intended for internal use only.
        /// </remarks>
        public bool ShouldRetry(bool enableEnhancedPreparedStatements)
        {
            RetryReason = GetRetryReason(enableEnhancedPreparedStatements);
            return RetryReason != RetryReason.NoRetry;
        }

        private RetryReason GetRetryReason(bool enableEnhancedPreparedStatements)
        {
            var error = Errors.FirstOrDefault();
            if (error != null)
            {
                if (error.Retry && error.Code != 12016)
                {
                    //If the server sends back retry even if not idempotent as long as its not 12016 (Index Not Found)
                    //https://docs.google.com/document/d/1vIDje29TEyNLMOQEPy6G0NbVzpHF7Op6tep1-xU2oGE/edit#heading=h.8yy151bwv1es
                    return RetryReason.QueryErrorRetryable;
                }
                if (enableEnhancedPreparedStatements)
                {
                    /*
                     * 4040 - Statement Not Found
                     * In the background the query engine is trying to fetch a prepared statement
                     * from a different node if it doesn’t have, but in the case it cannot find it
                     * from anywhere the client is supposed to re-prepare the statement on that
                     * specific node and execute again.
                     * Retryable: yes
                     * Action: run the fast prepare-and-execute logic on any of the nodes to make progress on the request.
                     * Other codes, especially 4001 (could not prepare a stale statement) need to be forward to the user.
                     */
                    if (error.Code == 4040)
                    {
                        return RetryReason.QueryPreparedStatementFailure;
                    }
                }
                else
                {
                    //pre-couchbase server 6.5 behavior
                    if (error.Code == 4050 || error.Code == 4070 ||
                        error.Code == 5000 && error.Message.Contains(QueryClient.Error5000MsgQueryPortIndexNotFound))
                    {
                        return RetryReason.QueryPreparedStatementFailure;
                    }

                    return RetryReason.NoRetry;
                }
            }

            return RetryReason.NoRetry;
        }

        public RetryReason RetryReason { get; protected set; } = RetryReason.NoRetry;

        public Exception? NoRetryException { get; set; }

        /// <summary>
        /// Get the prepared query plan name stored in the cluster.
        /// </summary>
        public string? PreparedPlanName { get; protected set; }

        /// <summary>
        /// Initializes the reader, and reads all attributes until result rows are encountered.
        /// This must be called before properties are valid.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task.</returns>
        public abstract Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public virtual void Dispose()
        {
            ResponseStream?.Dispose();
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
