using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    internal abstract class SystemTextJsonStreamReader : IJsonStreamReader
    {
        private const byte Utf8DecimalChar = 0x2e;

        private readonly Stream _stream;

        private JsonReaderState _state;
        private JsonBuffer _buffer;
        private JsonTokenType _tokenType = JsonTokenType.None;
        private bool _tokenHasDecimalPlace;

        // Tracks the path to the current property or array item.
        private readonly PathState _pathState = new();

        /// <inheritdoc />
        public int Depth { get; private set; }

        protected SystemTextJsonStreamReader(Stream stream, JsonSerializerOptions options)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (stream is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(stream));
            }
            if (options is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(options));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            _stream = stream;
            _state = new JsonReaderState(new JsonReaderOptions
            {
                AllowTrailingCommas = options.AllowTrailingCommas,
                CommentHandling = options.ReadCommentHandling,
                MaxDepth = options.MaxDepth
            });
            _buffer = new JsonBuffer(options.DefaultBufferSize);
        }

        #region Initialize

        /// <inheritdoc />
        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_buffer.IsStreamComplete || _buffer.UsedBytes > 0)
            {
                throw new InvalidOperationException("InitializeAsync should only be called once.");
            }

            await ReadFromStreamAsync(cancellationToken).ConfigureAwait(false);

            return _buffer.UsedBytes > 0;
        }

        #endregion

        #region ReadToNextAttribute

        /// <inheritdoc />
        public async Task<string?> ReadToNextAttributeAsync(CancellationToken cancellationToken = default)
        {
            while (!ReadToNextAttribute())
            {
                if (_buffer.IsStreamComplete)
                {
                    // No more properties found
                    return null;
                }

                // Read more data
                await ReadFromStreamAsync(cancellationToken).ConfigureAwait(false);
            }

            // Peek ahead and get the type of the next token, which is used for the ValueType property
            while (!PeekNextToken(out _tokenType, out _tokenHasDecimalPlace))
            {
                // Read more data
                await ReadFromStreamAsync(cancellationToken).ConfigureAwait(false);
            }

            return _pathState.Path;
        }

        private bool ReadToNextAttribute()
        {
            Utf8JsonReader reader = new(_buffer.CurrentSegment, _buffer.IsStreamComplete, _state);

            do
            {
                if (!reader.Read())
                {
                    break;
                }

                _pathState.ApplyReadToken(ref reader);
            } while (reader.TokenType != JsonTokenType.PropertyName);

            UpdateState(ref reader);
            return reader.TokenType == JsonTokenType.PropertyName;
        }

        #endregion

        #region ReadObject

        /// <inheritdoc />
        public async Task<T> ReadObjectAsync<T>(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (!ReadObject<T>(out var obj))
                {
                    await ReadFromStreamAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    return obj!;
                }
            }
        }

        private bool ReadObject<T>(out T? obj)
        {
            Utf8JsonReader reader = new(_buffer.CurrentSegment, _buffer.IsStreamComplete, _state);

            // Read the value or StartObject/StartArray
            if (!reader.Read())
            {
                // Don't update state, we'll read more from the stream and try again
                obj = default;
                return false;
            }

            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                // Try to skip the object, which will ensure that the entire object is in the buffer.
                // If we fail, we need to read more data. If we succeed, we don't call UpdateState so that
                // the skip is thrown out and we're back at the beginning of the object.
                Utf8JsonReader readerClone = reader;
                if (!readerClone.TrySkip())
                {
                    obj = default;
                    return false;
                }
            }
            else
            {
                // Try to read as a value. We could theoretically do this using Deserialize<T>, but this causes
                // limitations when using JsonSerializerContext via ContextSystemTextJsonStreamReader. It would
                // require that the JsonSerializerContext have the type T for basic types like string, long, etc
                // registered on it via attributes, which is cumbersome for the consumer.
                JsonElement element = JsonElement.ParseValue(ref reader);
                if (element.TryGetValue<T>(out var value))
                {
                    obj = value;
                    _pathState.ValueWasRead();
                    UpdateState(ref reader);
                    return true;
                }
            }

            obj = Deserialize<T>(ref reader);
            _pathState.ValueWasRead();
            UpdateState(ref reader);
            return true;
        }

        #endregion

        #region ReadArray

        /// <inheritdoc />
        public async IAsyncEnumerable<T> ReadArrayAsync<T>(
            Func<IJsonStreamReader, CancellationToken, Task<T>> readElement,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!ReadArrayStart())
            {
                await ReadFromStreamAsync(cancellationToken).ConfigureAwait(false);
            }

            while (true)
            {
                if (!PeekNextToken(out var nextTokenType, out _))
                {
                    // Read more data and try again
                    await ReadFromStreamAsync(cancellationToken).ConfigureAwait(false);
                }
                else if (nextTokenType == JsonTokenType.EndArray)
                {
                    yield break;
                }
                else
                {
                    yield return await readElement(this, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private bool ReadArrayStart()
        {
            Utf8JsonReader reader = new(_buffer.CurrentSegment, _buffer.IsStreamComplete, _state);

            if (!reader.Read())
            {
                return false;
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                ThrowHelper.ThrowInvalidOperationException("Reader is not positioned at the start of an array.");
            }

            _pathState.ApplyReadToken(ref reader);
            UpdateState(ref reader);
            return true;
        }

        #endregion

        #region ReadToken

        /// <inheritdoc />
        public async Task<IJsonToken> ReadTokenAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (!ReadToken(out var element))
                {
                    await ReadFromStreamAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    return new SystemTextJsonToken(element.GetValueOrDefault(), this);
                }
            }
        }

        private bool ReadToken(out JsonElement? element)
        {
            Utf8JsonReader reader = new(_buffer.CurrentSegment, _buffer.IsStreamComplete, _state);

            // Read the value or StartObject/StartArray
            if (!reader.Read())
            {
                // Don't update state, we'll read more from the stream and try again
                element = default;
                return false;
            }

            var result = JsonElement.TryParseValue(ref reader, out element);
            if (result)
            {
                UpdateState(ref reader);
                _pathState.ValueWasRead();
            }

            return result;
        }

        #endregion

        #region Value

        /// <inheritdoc />
        public Type? ValueType => _tokenType switch
        {
            JsonTokenType.String => typeof(string),
            JsonTokenType.True or JsonTokenType.False => typeof(bool),
            JsonTokenType.Number => _tokenHasDecimalPlace ? typeof(double) : typeof(long),
            _ => null
        };

        /// <inheritdoc />
        public object? Value
        {
            get
            {
                // For value types, the peek done by ReadToNextAttributeAsync will have already ensured
                // that an entire value is in the buffer.

                Utf8JsonReader reader = new(_buffer.CurrentSegment, _buffer.IsStreamComplete, _state);
                if (!reader.Read())
                {
                    return null;
                }

                return reader.TokenType switch
                {
                    JsonTokenType.String => reader.GetString(),
                    JsonTokenType.True => true,
                    JsonTokenType.False => false,
                    JsonTokenType.Number => ReaderHasDecimalPlace(ref reader)
                        ? (object) reader.GetDouble()
                        : (object) reader.GetInt64(),
                    _ => null
                };
            }
        }

        private bool PeekNextToken(out JsonTokenType nextTokenType, out bool tokenHasDecimalPlace)
        {
            Utf8JsonReader reader = new(_buffer.CurrentSegment, _buffer.IsStreamComplete, _state);

            if (!reader.Read())
            {
                nextTokenType = JsonTokenType.None;
                tokenHasDecimalPlace = false;
                return false;
            }

            nextTokenType = reader.TokenType;
            tokenHasDecimalPlace = reader.TokenType == JsonTokenType.Number
                                   && ReaderHasDecimalPlace(ref reader);

            return true;
        }

        private static bool ReaderHasDecimalPlace(ref Utf8JsonReader reader)
        {
            if (reader.HasValueSequence)
            {
                foreach (var segment in reader.ValueSequence)
                {
                    var span = segment.Span;
                    for (var i = 0; i < span.Length; i++)
                    {
                        if (span[i] == Utf8DecimalChar)
                        {
                            return true;
                        }
                    }
                }
            }
            else
            {
                for (var i = 0; i < reader.ValueSpan.Length; i++)
                {
                    if (reader.ValueSpan[i] == Utf8DecimalChar)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public abstract T? Deserialize<T>(JsonElement element);

        protected abstract T? Deserialize<T>(ref Utf8JsonReader reader);

        #endregion

        public void Dispose()
        {
            _buffer.Dispose();
            _buffer = default;

            _stream.Dispose();
        }

        #region Depth Stack

        private readonly record struct PathStateItem(string Path, int? ArrayIndex = null);

        /// <summary>
        /// Tracks the path to the current property or array item in the top of the stack.
        /// The strings for previous paths that lead to the current property or array item
        /// are stored in the layers of the stack to reduce string allocations as we navigate.
        /// </summary>
        private readonly struct PathState
        {
            private readonly Stack<PathStateItem> _stack = new(16);

            public PathState()
            {
            }

            public string Path => TryPeek(out var item) ? item.Path : "";

            public void ApplyReadToken(ref Utf8JsonReader reader)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                    {
                        var path = Path;
                        if (path.Length > 0)
                        {
                            // Only add a "." if this is not the root object
                            path += ".";
                        }

                        _stack.Push(new(path));
                        break;
                    }

                    case JsonTokenType.StartArray:
                        _stack.Push(new($"{Path}[0]", 0));
                        break;

                    case JsonTokenType.PropertyName:
                    {
                        var propertyName = reader.GetString()!;
                        _stack.Push(new(Path + propertyName));
                        break;
                    }

                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        _stack.Pop();
                        if (_stack.Count > 0)
                        {
                            // If we're not on the root object/array, advance
                            ValueWasRead();
                        }
                        break;

                    case JsonTokenType.Null:
                    case JsonTokenType.Number:
                    case JsonTokenType.String:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                        ValueWasRead();
                        break;
                }
            }

            public void ValueWasRead()
            {
                var item = _stack.Pop();

                if (item.ArrayIndex != null)
                {
                    // We're moving to the next item in an array
                    var newIndex = item.ArrayIndex + 1;
                    _stack.Push(new($"{Path}[{newIndex}]", newIndex));
                }
            }

            private bool TryPeek(out PathStateItem item)
            {
#if NETSTANDARD2_0
                if (_stack.Count > 0)
                {
                    item = _stack.Peek();
                    return true;
                }

                item = default;
                return false;
#else
                return _stack.TryPeek(out item);
#endif
            }
        }

        #endregion

        #region Stream Handling

        private void UpdateState(ref Utf8JsonReader reader)
        {
            _state = reader.CurrentState;
            _tokenType = reader.TokenType;
            Depth = reader.CurrentDepth;

            _buffer.ConsumeBytes((int) reader.BytesConsumed);
        }

        private async Task ReadFromStreamAsync(CancellationToken cancellationToken)
        {
            Debug.Assert(_buffer.Buffer != null);

            if (_buffer.IsStreamComplete)
            {
                ThrowHelper.ThrowInvalidOperationException("Unexpected end of stream.");
            }

            // Make sure the buffer is at least half-empty, otherwise grow the buffer
            _buffer.EnsureBufferSpace();

            while (true)
            {
                var writeIndex = _buffer.Offset + _buffer.UsedBytes;
                Debug.Assert(writeIndex < _buffer.Buffer!.Length);

#if SPAN_SUPPORT
                int readBytes = await _stream.ReadAsync(
                    _buffer.Buffer.AsMemory(writeIndex),
                    cancellationToken).ConfigureAwait(false);
#else
                int readBytes = await _stream.ReadAsync(
                    _buffer.Buffer,
                    writeIndex,
                    _buffer.Buffer.Length - writeIndex,
                    cancellationToken).ConfigureAwait(false);
#endif

                if (readBytes == 0)
                {
                    _buffer.IsStreamComplete = true;
                    return;
                }

                _buffer.UsedBytes += readBytes;

                if (_buffer.UsedBytes + _buffer.Offset >= _buffer.Buffer.Length)
                {
                    // The buffer is full, return for now. It will be cleared or grown as needed before the next call.
                    return;
                }
            }
        }

        private struct JsonBuffer : IDisposable
        {
            public byte[] Buffer;
            public int Offset;
            public int UsedBytes;
            public bool IsStreamComplete;

            public ReadOnlySpan<byte> CurrentSegment => Buffer.AsSpan(Offset, UsedBytes);

            public JsonBuffer(int bufferSize)
            {
                Buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                Offset = 0;
                UsedBytes = 0;
                IsStreamComplete = false;
            }

            public void ConsumeBytes(int bytesConsumed)
            {
                Debug.Assert(bytesConsumed <= UsedBytes);

                if (bytesConsumed == 0)
                {
                    return;
                }

                if (bytesConsumed == UsedBytes)
                {
                    // We've consumed all bytes, quick short-circuit to reset to the beginning of the buffer
                    Offset = 0;
                    UsedBytes = 0;
                    return;
                }

                Offset += bytesConsumed;
                UsedBytes -= bytesConsumed;
            }

            public void EnsureBufferSpace()
            {
                var halfOfBufferLength = (uint) Buffer.Length / 2;
                if ((uint) Offset >= halfOfBufferLength)
                {
                    // We're more than halfway into the buffer, time to shift it back to the beginning

                    System.Buffer.BlockCopy(Buffer, Offset, Buffer, 0, UsedBytes);
                    Offset = 0;
                }
                else if ((uint) (UsedBytes + Offset) > halfOfBufferLength)
                {
                    // We've used more than half of the buffer, grow it to make more room and shift to the beginning

                    byte[] oldBuffer = Buffer;
                    byte[] newBuffer = ArrayPool<byte>.Shared.Rent(oldBuffer.Length * 2);

                    System.Buffer.BlockCopy(oldBuffer, Offset, newBuffer, 0, UsedBytes);
                    Buffer = newBuffer;
                    Offset = 0;

                    ArrayPool<byte>.Shared.Return(oldBuffer);
                }
            }

            public void Dispose()
            {
                if (Buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(Buffer);
                }
            }
        }

        #endregion
    }
}
