using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Serializers;

#nullable enable

namespace Couchbase.Query
{
    /// <summary>
    /// Represents a streaming N1QL response for reading each item as they become available over the network.
    /// </summary>
    /// <typeparam name="T">A POCO that matches each row of the response.</typeparam>
    /// <seealso cref="IQueryResult{T}" />
    internal class StreamingQueryResult<T> : QueryResultBase<T>
    {
        private readonly IStreamingTypeDeserializer _deserializer;
        private IJsonStreamReader? _reader;
        private bool _hasReadToResult;
        private bool _hasReadResult;
        private bool _hasFinishedReading;

        /// <summary>
        /// Creates a new StreamingQueryResult.
        /// </summary>
        /// <param name="responseStream"><see cref="Stream"/> to read.</param>
        /// <param name="deserializer"><see cref="ITypeSerializer"/> used to deserialize objects.</param>
        public StreamingQueryResult(Stream responseStream, IStreamingTypeDeserializer deserializer)
            : base(responseStream)
        {
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _reader = _deserializer.CreateJsonStreamReader(ResponseStream);

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
        public override async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
#pragma warning restore 8425
        {
            if (_hasReadResult)
            {
                // Don't allow enumeration more than once

                throw new StreamAlreadyReadException();
            }
            if (_hasFinishedReading)
            {
                // empty collection OR non-success message
                // Known Issue: Since we have to read the entire message in non-success cases to get the errors,
                //              We can't stop the reader at the "results" section.  This shouldn't matter, since
                //              query error results won't have useful "results" populated.
                _hasReadResult = true;
                yield break;
            }
            if (!_hasReadToResult)
            {
                throw new InvalidOperationException(
                    $"{nameof(StreamingQueryResult<T>)} has not been initialized, call InitializeAsync first");
            }

            if (_reader == null)
            {
                // Should not be possible
                throw new InvalidOperationException("_reader is null");
            }

            // Read isn't complete, so the stream is currently waiting to deserialize the results

            await foreach (var result in _reader.ReadObjectsAsync<T>(cancellationToken).ConfigureAwait(false))
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
                        MetaData.Signature = (await _reader.ReadTokenAsync(cancellationToken).ConfigureAwait(false))
                            .ToDynamic();
                        break;
                    case "prepared" when _reader.ValueType == typeof(string):
                        PreparedPlanName = _reader.Value?.ToString();;
                        break;
                    case "profile":
                        MetaData.Profile = (await _reader.ReadTokenAsync(cancellationToken).ConfigureAwait(false))
                            .ToDynamic();
                        break;
                    case "metrics":
                        MetaData.Metrics =
                            (await _reader.ReadObjectAsync<MetricsData>(cancellationToken).ConfigureAwait(false))
                            .ToMetrics();
                        break;
                    case "results":
                        _hasReadToResult = true;

                        if (this.Success)
                        {
                            // We've reached the result rows, return now
                            return;
                        }
                        else
                        {
                            // In non-success situations, we want to populate all the error and warning fields.
                            break;
                        }
                    case "warnings":
                        await foreach (var warning in _reader.ReadObjectsAsync<QueryWarning>(cancellationToken)
                            .ConfigureAwait(false))
                        {
                            MetaData.Warnings.Add(warning);
                        }

                        break;
                    case "errors":
                        await foreach (var error in _reader.ReadObjectsAsync<Error>(cancellationToken)
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

        /// <inheritdoc />
        public override void Dispose()
        {
            _reader?.Dispose(); // also closes underlying stream
            _reader = null;

            base.Dispose();
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
