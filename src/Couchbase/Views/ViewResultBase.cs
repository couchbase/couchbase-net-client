using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Retry;
using OpenTracing;

#nullable enable

namespace Couchbase.Views
{
    /// <summary>
    /// Abstract base implementation of <seealso cref="IViewResult{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">Type of the key for each result row.</typeparam>
    /// <typeparam name="TValue">Type of the value for each result row.</typeparam>
    internal abstract class ViewResultBase<TKey, TValue> : IViewResult<TKey, TValue>
    {
        /// <summary>
        /// Creates a new ViewResultBase.
        /// </summary>
        /// <param name="statusCode">HTTP status code returned with result.</param>
        /// <param name="message">Message about result.</param>
        protected ViewResultBase(HttpStatusCode statusCode, string message)
        {
            StatusCode = statusCode;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        /// <summary>
        /// Creates a new ViewResultBase.
        /// </summary>
        /// <param name="statusCode">HTTP status code returned with result.</param>
        /// <param name="message">Message about result.</param>
        /// <param name="responseStream"><see cref="Stream"/> to read.</param>
        /// <param name="decodeSpan">Span to complete once decoding is done.</param>
        protected ViewResultBase(HttpStatusCode statusCode, string message, Stream responseStream,
            ISpan? decodeSpan = null)
            : this(statusCode, message)
        {
            ResponseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
            DecodeSpan = decodeSpan;
        }

        /// <inheritdoc />
        public IAsyncEnumerable<IViewRow<TKey, TValue>> Rows => this;

        /// <summary>
        /// Response stream being deserialized.
        /// </summary>
        protected Stream? ResponseStream { get; }

        protected ISpan? DecodeSpan { get; }
        public HttpStatusCode StatusCode { get; }
        public string Message { get; }

        /// <inheritdoc />
        public ViewMetaData? MetaData { get; protected set; }

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
            return RetryReason != RetryReason.NoRetry;
        }

        private void SetRetryReasonIfFailed()
        {
            // View status code retry strategy
            // https://docs.google.com/document/d/1GhRxvPb7xakLL4g00FUi6fhZjiDaP33DTJZW7wfSxrI/edit
            switch (StatusCode)
            {
                case HttpStatusCode.MultipleChoices: // 300
                case HttpStatusCode.MovedPermanently: // 301
                case HttpStatusCode.Found: // 302
                    RetryReason = RetryReason.ViewsNoActivePartition;
                    break;
                case HttpStatusCode.SeeOther: // 303
                case HttpStatusCode.TemporaryRedirect: //307
                case HttpStatusCode.Gone: //401
                case HttpStatusCode.RequestTimeout: // 408
                case HttpStatusCode.Conflict: // 409
                case HttpStatusCode.PreconditionFailed: // 412
                case HttpStatusCode.RequestedRangeNotSatisfiable: // 416
                case HttpStatusCode.ExpectationFailed: // 417
                case HttpStatusCode.NotImplemented: //501
                case HttpStatusCode.BadGateway: // 502
                case HttpStatusCode.ServiceUnavailable: // 503
                case HttpStatusCode.GatewayTimeout: // 504
                    RetryReason = RetryReason.ViewsTemporaryFailure;
                    break;
                case HttpStatusCode.NotFound: // 404
                    if (Message.Contains("\"reason\":\"missing\""))
                    {
                        RetryReason = RetryReason.ViewsTemporaryFailure;
                    }
                    break;
                case HttpStatusCode.InternalServerError: // 500
                    if(Message.Contains("error") && Message.Contains("{not_found, missing_named_view}") ||
                       Message.Contains("badarg"))
                    {
                        RetryReason = RetryReason.ViewsTemporaryFailure;
                    }
                    break;
                default:
                    RetryReason = RetryReason.NoRetry;
                    return;
            }
        }

        public RetryReason RetryReason { get; protected set; } = RetryReason.NoRetry;

        /// <summary>
        /// Initializes the reader, and reads all attributes until result rows are encountered.
        /// This must be called before properties are valid.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task.</returns>
        public abstract Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public abstract IAsyncEnumerator<IViewRow<TKey, TValue>> GetAsyncEnumerator(CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public virtual void Dispose()
        {
            ResponseStream?.Dispose();
        }
    }
}
