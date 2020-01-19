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
                ArrayPool = JsonArrayPool.Instance
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
                if (!await _reader.ReadAsync(cancellationToken))
                {
                    return null;
                }
            }

            // Read one more time to get to the beginning of the attribute value
            if (!await _reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return _reader.Path;
        }

        /// <inheritdoc />
        public async Task<T> ReadObjectAsync<T>(CancellationToken cancellationToken = default)
        {
            if (_reader.TokenType != JsonToken.StartObject)
            {
                throw new InvalidOperationException("Reader is not positioned at the start of an object.");
            }

            var jObject = await JToken.ReadFromAsync(_reader, cancellationToken)
                .ConfigureAwait(false);
            return jObject.ToObject<T>(Deserializer);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<T> ReadArrayAsync<T>(
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
                    yield return await ReadObjectAsync<T>(cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (_reader.Depth == initialDepth && _reader.TokenType == JsonToken.EndArray)
                {
                    break;
                }
            }
        }

        /// <inheritdoc />
        public async Task<dynamic> ReadTokenAsync(CancellationToken cancellationToken = default)
        {
            return await JToken.ReadFromAsync(_reader, cancellationToken);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _reader.Close();
        }
    }
}
