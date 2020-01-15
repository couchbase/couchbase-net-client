using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTracing;

namespace Couchbase.Query
{
    /// <summary>
    /// Represents a streaming N1QL response for reading each item as they become available over the network.
    /// </summary>
    /// <typeparam name="T">A POCO that matches each row of the response.</typeparam>
    /// <seealso cref="IQueryResult{T}" />
    public class StreamingQueryResult<T> : IQueryResult<T>
    {
        private JsonTextReader _reader;
        private string _preparedPlanName;
        private IAsyncEnumerable<T> _enumerator;

        internal ISpan DecodeSpan { get; set; }

        /// <summary>
        /// Gets the meta data associated with the analytics result.
        /// </summary>
        public QueryMetaData MetaData { get; internal set; }

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
        public string Message { get; internal set; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public Exception Exception { get; internal set; }

        

        /// <summary>
        /// If the response indicates the request is retryable, returns true.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Intended for internal use only.
        /// </remarks>
        public bool ShouldRetry()
        {
            var retry = false;
            switch (MetaData.Status)
            {
                case QueryStatus.Success:
                case QueryStatus.Errors:
                case QueryStatus.Running:
                case QueryStatus.Completed:
                case QueryStatus.Stopped:
                    break;
                case QueryStatus.Timeout:
                case QueryStatus.Fatal:
                    var status = (int)HttpStatusCode;
                    if (status > 399)
                    {
                        break;
                    }
                    retry = true;
                    break;
            }
            return retry;
        }

        /// <summary>
        /// Get the prepared query plan name stored in the cluster.
        /// </summary>
        public string PreparedPlanName
        {
            get => _preparedPlanName;
            set => _preparedPlanName = value;
        }

        /// <summary>
        /// Gets or sets the response stream.
        /// </summary>
        /// <value>
        /// The response stream.
        /// </value>
        internal Stream ResponseStream { get; set; }

        /// <summary>
        /// Indicates that the query result has been completely read.
        /// </summary>
        internal bool HasFinishedReading { get; private set; }

        /// <summary>
        /// Initializes the reader, and reads all attributes until result rows are encountered.
        /// This must be called before properties are valid.
        /// </summary>
        internal async Task ReadToRowsAsync(CancellationToken cancellationToken)
        {
            _reader = new JsonTextReader(new StreamReader(ResponseStream));

            // Read the attributes until we reach the end of the object or the "results" attribute
            await ReadResponseAttributes(cancellationToken).ConfigureAwait(false);

            if (!HasFinishedReading)
            {
                // We encountered a results attribute, so we must be successful
                // We'll assume so until we read otherwise later

                Success = true;

                _enumerator = new QueryResultRows<T>(this, _reader);
            }
            else
            {
                _enumerator = AsyncEnumerable.Empty<T>();
            }
        }

        /// <inheritdoc />
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            if (_enumerator == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(StreamingQueryResult<T>)} has not been initialized, call ReadToRowsAsync first");
            }
            return _enumerator.GetAsyncEnumerator(cancellationToken);
        }

        /// <summary>
        /// Reads and parses any response attributes, returning at the end of the response or
        /// once the "results" attribute is encountered.
        /// </summary>
        internal async Task ReadResponseAttributes(CancellationToken cancellationToken)
        {
            MetaData = new QueryMetaData();

            while (await _reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (_reader.Path)
                {
                    case "requestID" when _reader.TokenType == JsonToken.String:
                        MetaData.RequestId = _reader.Value.ToString();
                        break;
                    case "status" when _reader.TokenType == JsonToken.String:
                        if (Enum.TryParse(_reader.Value.ToString(), true, out QueryStatus status))
                        {
                            MetaData.Status = status;
                            Success = status == QueryStatus.Success;
                        }

                        break;
                    case "clientContextID" when _reader.TokenType == JsonToken.String:
                        MetaData.ClientContextId = _reader.Value.ToString();
                        break;
                    case "signature":
                        MetaData.Signature = JToken.ReadFrom(_reader);
                        break;
                    case "prepared" when _reader.TokenType == JsonToken.String:
                        _preparedPlanName = _reader.Value.ToString();;
                        break;
                    case "profile":
                        MetaData.Profile = JToken.ReadFrom(_reader);
                        break;
                    case "metrics.elapsedTime" when _reader.TokenType == JsonToken.String:
                        MetaData.Metrics.ElaspedTime = _reader.Value.ToString();
                        break;
                    case "metrics.executionTime" when _reader.TokenType == JsonToken.String:
                        MetaData.Metrics.ExecutionTime = _reader.Value.ToString();
                        break;
                    case "metrics.resultCount" when _reader.TokenType == JsonToken.Integer:
                        if (uint.TryParse(_reader.Value.ToString(), out var resultCount))
                        {
                            MetaData.Metrics.ResultCount = resultCount;
                        }

                        break;
                    case "metrics.resultSize" when _reader.TokenType == JsonToken.Integer:
                        if (uint.TryParse(_reader.Value.ToString(), out var resultSize))
                        {
                            MetaData.Metrics.ResultSize = resultSize;
                        }

                        break;
                    case "results":
                        // We've reached the result rows, return now

                        return;
                    case "warnings":
                        while (_reader.Read())
                        {
                            if (_reader.Depth == 2 && _reader.TokenType == JsonToken.StartObject)
                            {
                                MetaData.Warnings.Add(await ReadItem<QueryWarning>(_reader, cancellationToken)
                                    .ConfigureAwait(false));
                            }
                            if (_reader.Path == "warnings" && _reader.TokenType == JsonToken.EndArray)
                            {
                                break;
                            }
                        }

                        break;
                    case "errors":
                        while (_reader.Read())
                        {
                            if (_reader.Depth == 2 && _reader.TokenType == JsonToken.StartObject)
                            {
                                Errors.Add(await ReadItem<Error>(_reader, cancellationToken)
                                    .ConfigureAwait(false));
                            }
                            if (_reader.Path == "errors" && _reader.TokenType == JsonToken.EndArray)
                            {
                                break;
                            }
                        }

                        break;
                }
            }

            // We've reached the end of the object, mark that entire read is complete
            HasFinishedReading = true;
        }

        /// <summary>
        /// Reads the object at the current index within the reader.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <param name="jtr">The JTR.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns></returns>
        private static async Task<K> ReadItem<K>(JsonTextReader jtr, CancellationToken cancellationToken)
        {
            var jObject = await JToken.ReadFromAsync(jtr, cancellationToken)
                .ConfigureAwait(false);
            return jObject.ToObject<K>();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _reader?.Close(); // also closes underlying stream
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
