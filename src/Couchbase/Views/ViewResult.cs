using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Core.Exceptions;
using OpenTracing;

#nullable enable

namespace Couchbase.Views
{
    /// <summary>
    /// Represents a streaming View response for reading each row as it becomes available over the network.
    /// Note that unless there is no underlying collection representing the response, instead the rows are extracted
    /// from the stream one at a time. If the Enumeration is evaluated, eg calling ToListAsync(), then the entire response
    /// will be read. Once a row has been read from the stream, it is not available to be read again.
    /// A <see cref="StreamAlreadyReadException"/> will be thrown if the result is enumerated after it has reached
    /// the end of the stream.
    /// </summary>
    /// <seealso cref="IViewResult" />
    internal class ViewResult : IViewResult
    {
        private readonly Stream? _responseStream;
        private readonly IStreamingTypeDeserializer _deserializer;
        private readonly ISpan? _decodeSpan;

        private IJsonStreamReader? _reader;
        private bool _hasReadToRows;
        private bool _hasReadRows;
        private bool _hasFinishedReading;

        public ViewResult(HttpStatusCode statusCode, string message, IStreamingTypeDeserializer deserializer)
        {
            StatusCode = statusCode;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        public ViewResult(HttpStatusCode statusCode, string message, Stream responseStream, IStreamingTypeDeserializer deserializer,
            ISpan? decodeSpan = null)
            : this(statusCode, message, deserializer)
        {
            _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
            _decodeSpan = decodeSpan;
        }

        /// <inheritdoc />
        public IAsyncEnumerable<IViewRow> Rows => this;

        public HttpStatusCode StatusCode { get; }
        public string Message { get; }

        /// <inheritdoc />
        public ViewMetaData? MetaData { get; private set; }

        /// <summary>
        /// Initializes the reader, and reads all attributes until result rows are encountered.
        /// This must be called before properties are valid.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task.</returns>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_responseStream == null)
            {
                _hasFinishedReading = true;
                return;
            }
            if (_reader != null)
            {
                throw new InvalidOperationException("Cannot initialize more than once.");
            }

            _reader = _deserializer.CreateJsonStreamReader(_responseStream);
            if (!await _reader.InitializeAsync(cancellationToken).ConfigureAwait(false))
            {
                _hasFinishedReading = true;
                return;
            }

            // Read the attributes until we reach the end of the object or the "rows" attribute
            await ReadResponseAttributes(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
#pragma warning disable 8425
        public async IAsyncEnumerator<IViewRow> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore 8425
        {
            if (_hasReadRows)
            {
                // Don't allow enumeration more than once

                throw new StreamAlreadyReadException();
            }
            if (_hasFinishedReading)
            {
                // empty collection
                _hasReadRows = true;
                yield break;
            }
            if (!_hasReadToRows)
            {
                throw new InvalidOperationException(
                    $"{nameof(ViewResult)} has not been initialized, call InitializeAsync first");
            }

            if (_reader == null)
            {
                // Should not be possible
                throw new InvalidOperationException("_reader is null");
            }

            await foreach (var row in _reader.ReadTokensAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return new ViewRow
                {
                    Id = row["id"]?.Value<string>(),
                    KeyToken = row["key"],
                    ValueToken = row["value"]
                };
            }

            // we've reached the end of the stream, so mark it as finished reading
            _hasReadRows = true;

            // Read any remaining attributes after the results
            await ReadResponseAttributes(cancellationToken).ConfigureAwait(false);

            // if we have a decode span, finish it
            _decodeSpan?.Finish();
        }

        internal bool ShouldRetry()
        {
            SetRetryReasonIfFailed();
            return RetryReason != RetryReason.NoRetry;
        }

        internal void SetRetryReasonIfFailed()
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
        /// Reads and parses any response attributes, returning at the end of the response or
        /// once the "results" attribute is encountered.
        /// </summary>
        internal async Task ReadResponseAttributes(CancellationToken cancellationToken)
        {
            if (_reader == null)
            {
                // Should not be possible
                throw new InvalidOperationException("_reader is null");
            }

            if (MetaData == null)
            {
                MetaData = new ViewMetaData();
            }

            while (true)
            {
                var path = await _reader.ReadToNextAttributeAsync(cancellationToken).ConfigureAwait(false);
                if (path == null)
                {
                    // End of stream
                    break;
                }

                if (path == "total_rows" && _reader.Value != null && _reader.ValueType == typeof(long))
                {
                    MetaData.TotalRows = (uint) (long) _reader.Value;
                }
                else if (path == "rows")
                {
                    // we've reached the 'rows' element, return now so when we enumerate we start from here
                    _hasReadToRows = true;
                    return;
                }
            }

            // if we got here, there was no 'rows' element in the stream
            _hasFinishedReading = true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _reader?.Dispose(); // also closes underlying stream
            _reader = null;

            _responseStream?.Dispose();
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
