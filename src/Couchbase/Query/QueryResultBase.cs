using System;
using System.Collections.Generic;
using System.IO;
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
    internal abstract class QueryResultBase<T> : IQueryResult<T>
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
        public bool ShouldRetry()
        {
            SetRetryReasonIfFailed();
            return ((IServiceResult)this).RetryReason != RetryReason.NoRetry;
        }

        private void SetRetryReasonIfFailed()
        {
            foreach (var error in Errors)
            {
                switch (error.Code)
                {
                    case 4040:
                    case 4050:
                    case 4070:
                        ((IServiceResult) this).RetryReason = RetryReason.QueryPreparedStatementFailure;
                        return;
                    case 5000:
                        if (error.Message != null
                            && error.Message.Contains(QueryClient.Error5000MsgQueryPortIndexNotFound))
                        {
                            ((IServiceResult)this).RetryReason = RetryReason.QueryPreparedStatementFailure;
                        }
                        return;
                    default:
                        continue;
                }
            }
        }

        RetryReason IServiceResult.RetryReason { get; set; } = RetryReason.NoRetry;

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
