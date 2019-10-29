using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Retry;
using Couchbase.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTracing;

namespace Couchbase.Views
{
    /// <summary>
    /// Represents a streaming View response for reading each row as it becomes available over the network.
    /// Note that unless there is no underlying collection representing the response, instead the rows are extracted
    /// from the stream one at a time. If the Enumeration is evaluated, eg calling ToList(), then the entire response
    /// will be read. Once a row has been read from the stream, it is not available to be read again.
    /// A <see cref="StreamAlreadyReadException"/> will be thrown if the result is enumerated after it has reached
    /// the end of the stream.
    /// </summary>
    /// <typeparam name="T">A POCO that matches each row of the response.</typeparam>
    /// <seealso cref="IViewResult{T}" />
    internal class ViewResult : IViewResult
    {
        private readonly Result _result;

        public ViewResult(HttpStatusCode statusCode, string message)
        {
            StatusCode = statusCode;
            Message = message;
        }

        public ViewResult(HttpStatusCode statusCode, string message, Stream responseStream, ISpan decodeSpan = null)
            : this(statusCode, message)
        {
            _result = new Result(responseStream, decodeSpan);
        }

        public HttpStatusCode StatusCode { get; }
        public string Message { get; }

        private ViewMetaData _metaData;

        public ViewMetaData MetaData
        {
            get
            {
                if (!_result.HasFinishedReading)
                {
                    // have to finish reading before using meta
                    throw new InvalidOperationException();
                }

                if (_metaData == null)
                {
                    _metaData = new ViewMetaData {TotalRows = _result.TotalRows};
                }

                return _metaData;
            }
        }

        public IAsyncEnumerator<IViewRow> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return _result.GetAsyncEnumerator(cancellationToken);
        }

        internal bool ShouldRetry()
        {
            SetRetryReasonIfFailed();
            return ((IServiceResult)this).RetryReason != RetryReason.NoRetry;
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
                    ((IServiceResult)this).RetryReason = RetryReason.ViewsNoActivePartition;
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
                    ((IServiceResult)this).RetryReason = RetryReason.ViewsTemporaryFailure;
                    break;
                case HttpStatusCode.NotFound: // 404
                    if (Message.Contains("\"reason\":\"missing\""))
                    {
                        ((IServiceResult)this).RetryReason = RetryReason.ViewsTemporaryFailure;
                    }
                    break;
                case HttpStatusCode.InternalServerError: // 500
                    if(Message.Contains("error") && Message.Contains("{not_found, missing_named_view}") ||
                       Message.Contains("badarg"))
                    {
                        ((IServiceResult) this).RetryReason = RetryReason.ViewsTemporaryFailure;
                    }
                    break;
                default:
                    ((IServiceResult) this).RetryReason = RetryReason.NoRetry;
                    return;
            }
        }

        RetryReason IServiceResult.RetryReason { get; set; } = RetryReason.NoRetry;

        private class Result : IAsyncEnumerable<IViewRow>, IDisposable
        {
            private readonly Stream _responseStream;
            private readonly ISpan _decodeSpan;
            private JsonTextReader _reader;
            private uint _totalRows;

            internal volatile bool HasFinishedReading;

            public Result(Stream responseStream, ISpan decodeSpan)
            {
                _responseStream = responseStream;
                _decodeSpan = decodeSpan;
            }

            public uint TotalRows => _totalRows;

#pragma warning disable 8425
            public async IAsyncEnumerator<IViewRow> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore 8425
            {
                if (HasFinishedReading)
                {
                    throw new StreamAlreadyReadException();
                }

                // make sure we're are the 'rows' element in the stream
                if (await ReadToRowsAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await _reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (_reader.TokenType == JsonToken.StartObject)
                        {
                            var json = await JToken.ReadFromAsync(_reader, cancellationToken).ConfigureAwait(false);

                            yield return new ViewRow
                            {
                                Id = json.Value<string>("id"),
                                KeyToken = json["key"],
                                ValueToken = json["value"]
                            };
                        }
                    }
                }

                // we've reached the end of the stream, so mark it as finished reading
                HasFinishedReading = true;

                // if we have a decode span, finish it
                _decodeSpan?.Finish();
            }

            private async Task<bool> ReadToRowsAsync(CancellationToken cancellationToken)
            {
                // if reader is not null, we've already started to read the stream
                if (_reader != null)
                {
                    return true;
                }

                _reader = new JsonTextReader(new StreamReader(_responseStream));
                while (await _reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (_reader.Path == "total_rows" && _reader.TokenType == JsonToken.Integer)
                    {
                        uint totalRows;
                        if (uint.TryParse(_reader.Value.ToString(), out totalRows))
                        {
                            _totalRows = totalRows;
                        }
                    }
                    else if (_reader.Path == "rows" && _reader.TokenType == JsonToken.StartArray)
                    {
                        // we've reached the 'rows' element, return now so when we enumerate we start from here
                        return true;
                    }
                }

                // if we got here, there was no 'rows' element in the stream
                HasFinishedReading = true;
                return false;
            }

            public void Dispose()
            {
                _reader?.Close(); // also closes underlying stream
                _reader = null;
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
