using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
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
    /// <typeparam name="TKey">Type of the key for each result row.</typeparam>
    /// <typeparam name="TValue">Type of the value for each result row.</typeparam>
    /// <seealso cref="IViewResult{TKey, TValue}" />
    internal class StreamingViewResult<TKey, TValue> : ViewResultBase<TKey, TValue>
    {
        private readonly IStreamingTypeDeserializer _deserializer;

        private IJsonStreamReader? _reader;
        private bool _hasReadToRows;
        private bool _hasReadRows;
        private bool _hasFinishedReading;

        public StreamingViewResult(HttpStatusCode statusCode, string message, IStreamingTypeDeserializer deserializer)
            : base(statusCode, message)
        {
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        public StreamingViewResult(HttpStatusCode statusCode, string message, Stream responseStream, IStreamingTypeDeserializer deserializer,
            ISpan? decodeSpan = null)
            : base(statusCode, message, responseStream, decodeSpan)
        {
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (ResponseStream == null)
            {
                _hasFinishedReading = true;
                return;
            }
            if (_reader != null)
            {
                throw new InvalidOperationException("Cannot initialize more than once.");
            }

            _reader = _deserializer.CreateJsonStreamReader(ResponseStream);
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
        public override async IAsyncEnumerator<IViewRow<TKey, TValue>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
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
                    $"{nameof(StreamingViewResult<TKey, TValue>)} has not been initialized, call InitializeAsync first");
            }

            if (_reader == null)
            {
                // Should not be possible
                throw new InvalidOperationException("_reader is null");
            }

            await foreach (var row in _reader.ReadObjectsAsync<ViewRowData>(cancellationToken).ConfigureAwait(false))
            {
                yield return new ViewRow<TKey, TValue>
                {
                    Id = row.id,
                    Key = row.key,
                    Value = row.value
                };
            }

            // we've reached the end of the stream, so mark it as finished reading
            _hasReadRows = true;

            // Read any remaining attributes after the results
            await ReadResponseAttributes(cancellationToken).ConfigureAwait(false);

            // if we have a decode span, finish it
            DecodeSpan?.Finish();
        }

        /// <summary>
        /// Reads and parses any response attributes, returning at the end of the response or
        /// once the "results" attribute is encountered.
        /// </summary>
        private async Task ReadResponseAttributes(CancellationToken cancellationToken)
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
        public override void Dispose()
        {
            _reader?.Dispose(); // also closes underlying stream
            _reader = null;

            base.Dispose();
        }

        // ReSharper disable ClassNeverInstantiated.Local
        // ReSharper disable InconsistentNaming
        // ReSharper disable UnusedAutoPropertyAccessor.Local
        // ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
        private class ViewRowData
        {
            public string? id { get; set; }
            [AllowNull] public TKey key { get; set; } = default!;
            [AllowNull] public TValue value { get; set; } = default!;
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
