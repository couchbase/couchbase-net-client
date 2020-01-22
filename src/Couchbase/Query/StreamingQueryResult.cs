using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using OpenTracing;

#nullable enable

namespace Couchbase.Query
{
    /// <summary>
    /// Represents a streaming N1QL response for reading each item as they become available over the network.
    /// </summary>
    /// <typeparam name="T">A POCO that matches each row of the response.</typeparam>
    /// <seealso cref="IQueryResult{T}" />
    public class StreamingQueryResult<T> : IQueryResult<T>, IServiceResult
    {
        private readonly Stream _responseStream;
        private readonly IStreamingTypeDeserializer _deserializer;
        private IJsonStreamReader? _reader;
        private bool _hasReadToResult;
        private bool _hasReadResult;
        private bool _hasFinishedReading;

        internal ISpan? DecodeSpan { get; set; }

        /// <summary>
        /// Gets the meta data associated with the analytics result.
        /// </summary>
        public QueryMetaData? MetaData { get; internal set; }

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        /// <value>
        /// The HTTP status code.
        /// </value>
        internal HttpStatusCode HttpStatusCode { get; set; }

        /// <summary>
        /// Gets a list of 0 or more error objects; if an error occurred during processing of the request, it will be represented by an error object in this list.
        /// </summary>
        /// <value>
        /// The errors.
        /// </value>
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
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public Exception? Exception { get; internal set; }

        /// <summary>
        /// If the response indicates the request is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Intended for internal use only.
        /// </remarks>
        internal bool ShouldRetry()
        {
            SetRetryReasonIfFailed();
            return ((IServiceResult)this).RetryReason != RetryReason.NoRetry;
        }

        internal void SetRetryReasonIfFailed()
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
        public string? PreparedPlanName { get; set; }

        /// <summary>
        /// Creates a new StreamingQueryResult.
        /// </summary>
        /// <param name="responseStream"><see cref="Stream"/> to read.</param>
        /// <param name="deserializer"><see cref="ITypeSerializer"/> used to deserialize objects.</param>
        public StreamingQueryResult(Stream responseStream, IStreamingTypeDeserializer deserializer)
        {
            _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        /// <summary>
        /// Initializes the reader, and reads all attributes until result rows are encountered.
        /// This must be called before properties are valid.
        /// </summary>
        internal async Task ReadToRowsAsync(CancellationToken cancellationToken)
        {
            _reader = _deserializer.CreateJsonStreamReader(_responseStream);

            if (!await _reader.InitializeAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            // Read the attributes until we reach the end of the object or the "results" attribute
            await ReadResponseAttributes(cancellationToken).ConfigureAwait(false);

            if (!_hasFinishedReading)
            {
                // We encountered a results attribute, so we must be successful
                // We'll assume so until we read otherwise later

                Success = true;
            }
        }

        /// <inheritdoc />
#pragma warning disable 8425
        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore 8425
        {
            if (_hasReadResult)
            {
                // Don't allow enumeration more than once

                throw new StreamAlreadyReadException();
            }
            if (_hasFinishedReading)
            {
                // empty collection
                _hasReadResult = true;
                yield break;
            }
            if (!_hasReadToResult)
            {
                throw new InvalidOperationException(
                    $"{nameof(StreamingQueryResult<T>)} has not been initialized, call ReadToRowsAsync first");
            }

            if (_reader == null)
            {
                // Should not be possible
                throw new InvalidOperationException("_reader is null");
            }

            // Read isn't complete, so the stream is currently waiting to deserialize the results

            await foreach (var result in _reader.ReadArrayAsync<T>(cancellationToken).ConfigureAwait(false))
            {
                yield return result;
            }

            _hasReadResult = true;

            // Read any remaining attributes after the results
            await ReadResponseAttributes(cancellationToken).ConfigureAwait(false);
        }

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
                MetaData = new QueryMetaData();
            }

            _hasReadToResult = false;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var path = await _reader!.ReadToNextAttributeAsync(cancellationToken).ConfigureAwait(false);
                if (path == null)
                {
                    // Reached the end
                    break;
                }

                switch (path)
                {
                    case "requestID" when _reader.ValueType == typeof(string):
                        MetaData.RequestId = _reader.Value?.ToString();
                        break;
                    case "status" when _reader.ValueType == typeof(string):
                        if (Enum.TryParse(_reader.Value?.ToString(), true, out QueryStatus status))
                        {
                            MetaData.Status = status;
                            Success = status == QueryStatus.Success;
                        }

                        break;
                    case "clientContextID" when _reader.ValueType == typeof(string):
                        MetaData.ClientContextId = _reader.Value?.ToString();
                        break;
                    case "signature":
                        MetaData.Signature = await _reader.ReadTokenAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    case "prepared" when _reader.ValueType == typeof(string):
                        PreparedPlanName = _reader.Value?.ToString();;
                        break;
                    case "profile":
                        MetaData.Profile = await _reader.ReadTokenAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    case "metrics":
                        MetaData.Metrics =
                            (await _reader.ReadObjectAsync<MetricsData>(cancellationToken).ConfigureAwait(false))
                            .ToMetrics();
                        break;
                    case "results":
                        // We've reached the result rows, return now
                        _hasReadToResult = true;

                        return;
                    case "warnings":
                        await foreach (var warning in _reader.ReadArrayAsync<QueryWarning>(cancellationToken)
                            .ConfigureAwait(false))
                        {
                            MetaData.Warnings.Add(warning);
                        }

                        break;
                    case "errors":
                        await foreach (var error in _reader.ReadArrayAsync<Error>(cancellationToken)
                            .ConfigureAwait(false))
                        {
                            Errors.Add(error);
                        }

                        break;
                }
            }

            // We've reached the end of the object, mark that entire read is complete
            _hasFinishedReading = true;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _reader?.Dispose(); // also closes underlying stream
            _reader = null;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
