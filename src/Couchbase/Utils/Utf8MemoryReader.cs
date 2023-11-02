using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace Couchbase.Utils
{
    internal sealed class Utf8MemoryReader : TextReader
    {
        public static readonly ObjectPool<Utf8MemoryReader> InstancePool =
            ObjectPool.Create(new Utf8MemoryReaderPooledObjectPolicy());

        private readonly Decoder _decoder;
        private ReadOnlyMemory<byte> _memory;
        private char? _holdoverCharacter;

        public Utf8MemoryReader()
        {
            _decoder = Encoding.UTF8.GetDecoder();
        }

        public void ReleaseMemory()
        {
            _memory = default;
        }

        public void SetMemory(in ReadOnlyMemory<byte> buffer)
        {
            _memory = buffer;
            _decoder.Reset();
            _holdoverCharacter = null;
        }

        public override
#if !SPAN_SUPPORT
            unsafe
#endif
            int Read(char[] buffer, int index, int count)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (buffer is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(buffer));
            }
            if (buffer.Length < index + count)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
            }

            if (count == 0)
            {
                return 0;
            }

            var charsRead = 0;
            if (_holdoverCharacter != null)
            {
                buffer[index] = _holdoverCharacter.Value;
                _holdoverCharacter = null;
                index += 1;
                charsRead = 1;
            }

            if (_memory.Length == 0 || charsRead >= count)
            {
                return charsRead;
            }

            var span = _memory.Span;

#if SPAN_SUPPORT
            var destination = buffer.AsSpan(index, count);
            _decoder.Convert(span, destination, false, out var bytesRead, out var converterCharsRead, out var completed);
            charsRead += converterCharsRead;

            if (charsRead < count && !completed)
            {
                // We have encountered a surrogate pair along the boundary. Newtonsoft.Json will just request another read with
                // an insufficient buffer size. Instead we need to read the entire surrogate pair and give back the partial
                // surrogate pair.

                Span<char> tempBuffer = stackalloc char[2];

                _decoder.Convert(span.Slice(bytesRead), tempBuffer, false, out var tempBytesRead, out converterCharsRead,
                    out completed);
                bytesRead += tempBytesRead;

                if (converterCharsRead > 0)
                {
                    buffer[charsRead] = tempBuffer[0];
                    charsRead++;
                }
                if (converterCharsRead == 2)
                {
                    _holdoverCharacter = tempBuffer[1];
                }
            }
#else
            int bytesRead;

            fixed (char* destinationChars = &buffer[index])
            {
                fixed (byte* sourceBytes = &MemoryMarshal.GetReference(span))
                {
                    _decoder.Convert(sourceBytes, span.Length, destinationChars, count, false,
                        out bytesRead, out var converterCharsRead, out var completed);
                    charsRead += converterCharsRead;

                    if (charsRead < count && !completed)
                    {
                        // We have encountered a surrogate pair along the boundary. Newtonsoft.Json will just request another read with
                        // an insufficient buffer size. Instead we need to read the entire surrogate pair and give back the partial
                        // surrogate pair.

                        // This is a rare case so we don't retain this small buffer for reuse
                        var tempBuffer = new char[2];

                        fixed (char* tempBufferChars = &tempBuffer[0])
                        {
                            _decoder.Convert(sourceBytes + bytesRead, span.Length - bytesRead, tempBufferChars, tempBuffer.Length, false,
                                out var tempBytesRead, out converterCharsRead, out completed);
                            bytesRead += tempBytesRead;

                            if (converterCharsRead > 0)
                            {
                                buffer[charsRead] = tempBuffer[0];
                                charsRead++;
                            }

                            if (converterCharsRead == 2)
                            {
                                _holdoverCharacter = tempBuffer[1];
                            }
                        }
                    }
                }
            }
#endif

            _memory = _memory.Slice(bytesRead);
            return charsRead;
        }

        private sealed class Utf8MemoryReaderPooledObjectPolicy : DefaultPooledObjectPolicy<Utf8MemoryReader>
        {
            public override bool Return(Utf8MemoryReader obj)
            {
                obj.ReleaseMemory();
                return true;
            }
        }
    }
}
