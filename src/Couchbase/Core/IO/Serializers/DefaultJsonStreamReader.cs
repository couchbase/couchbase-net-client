using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Newtonsoft.Json based implementation of <see cref="IJsonStreamReader"/>.
    /// </summary>
    public class DefaultJsonStreamReader : IJsonStreamReader
    {
        private readonly JsonTextReader _reader;

        /// <summary>
        /// The <see cref="JsonSerializer"/> to use for deserializing objects.
        /// </summary>
        public JsonSerializer Deserializer { get; }

        /// <inheritdoc />
        public Type? ValueType => _reader.ValueType;

        /// <inheritdoc />
        public object? Value => _reader.Value;

        /// <inheritdoc />
        public int Depth => _reader.Depth;

        /// <summary>
        /// Creates a new DefaultJsonStreamReader.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="deserializer">The <see cref="JsonSerializer"/> to use for deserializing objects.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="deserializer"/> is null.</exception>
        public DefaultJsonStreamReader(Stream stream, JsonSerializer deserializer)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            Deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));

            _reader = new JsonTextReader(new StreamReader(stream))
            {
                ArrayPool = JsonArrayPool.Instance,
                DateParseHandling = deserializer.DateParseHandling,
                DateFormatString = deserializer.DateFormatString,
                DateTimeZoneHandling = deserializer.DateTimeZoneHandling,
                FloatParseHandling = deserializer.FloatParseHandling,
                MaxDepth = deserializer.MaxDepth,
                Culture = deserializer.Culture
            };
        }

        /// <inheritdoc />
        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_reader.TokenType != JsonToken.None)
            {
                throw new InvalidOperationException("InitializeAsync should only be called once.");
            }

            // We don't need to await here, we could return the task
            // But awaiting will include InitializeAsync on exception stack traces
            return await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<string?> ReadToNextAttributeAsync(CancellationToken cancellationToken = default)
        {
            while (_reader.TokenType != JsonToken.PropertyName)
            {
                if (!await _reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }
            }

            // Read one more time to get to the beginning of the attribute value
            if (!await _reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return _reader.Path;
        }

        /// <inheritdoc />
        public Task<T> ReadObjectAsync<T>(CancellationToken cancellationToken = default) =>
            Task.FromResult(Deserializer.Deserialize<T>(_reader));

        /// <inheritdoc />
        public async IAsyncEnumerable<T> ReadArrayAsync<T>(
            Func<IJsonStreamReader, CancellationToken, Task<T>> readElement,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_reader.TokenType != JsonToken.StartArray)
            {
                throw new InvalidOperationException("Reader is not positioned at the start of an array.");
            }

            var initialDepth = _reader.Depth;

            while (await _reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_reader.Depth > initialDepth)
                {
                    yield return await readElement(this, cancellationToken).ConfigureAwait(false);
                }
                else if (_reader.Depth == initialDepth && _reader.TokenType == JsonToken.EndArray)
                {
                    break;
                }
            }
        }

        /// <inheritdoc />
        public async Task<IJsonToken> ReadTokenAsync(CancellationToken cancellationToken = default)
        {
            return new NewtonsoftJsonToken(
                await JToken.ReadFromAsync(_reader, cancellationToken).ConfigureAwait(false),
                Deserializer);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _reader.Close();
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
