using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.ObjectPool;

namespace Couchbase.Utils
{
    internal sealed class Utf8MemoryReader : TextReader
    {
        public static readonly ObjectPool<Utf8MemoryReader> InstancePool =
            ObjectPool.Create(new Utf8MemoryReaderPooledObjectPolicy());

        private readonly Decoder _decoder;
        private ReadOnlySequence<byte> _sequence;
        private char? _holdoverCharacter;

        public Utf8MemoryReader()
        {
            _decoder = Encoding.UTF8.GetDecoder();
        }

        public void ReleaseMemory()
        {
            _sequence = default;
        }

        public void SetSequence(ReadOnlySequence<byte> buffer)
        {
            _sequence = buffer;
            _decoder.Reset();
            _holdoverCharacter = null;
        }

        public void SetMemory(ReadOnlyMemory<byte> buffer) =>
            SetSequence(new ReadOnlySequence<byte>(buffer));

        /// <summary>
        /// Reads as many characters as possible from a span of bytes into a span of characters.
        /// </summary>
        /// <param name="source">Source bytes.</param>
        /// <param name="destination">Destination characters.</param>
        /// <param name="charsWritten">Number of characters written to <paramref name="destination"/>.</param>
        /// <returns>Number of bytes read from <paramref name="source"/>.</returns>
        private
#if !SPAN_SUPPORT
            unsafe
#endif
            int Read(ReadOnlySpan<byte> source, Span<char> destination, out int charsWritten)
        {
            charsWritten = 0;

            if (destination.Length == 0)
            {
                return 0;
            }

            if (_holdoverCharacter != null)
            {
                destination[0] = _holdoverCharacter.Value;
                _holdoverCharacter = null;
                destination = destination.Slice(1);
                charsWritten = 1;

                if (destination.Length == 0)
                {
                    // We filled the destination with the holdover character
                    return 0;
                }
            }

            if (source.Length == 0)
            {
                return 0;
            }

#if SPAN_SUPPORT
            _decoder.Convert(source, destination, false, out var bytesRead, out var converterCharsRead, out var completed);
            charsWritten += converterCharsRead;

            if (converterCharsRead < destination.Length && !completed)
            {
                // We have encountered a surrogate pair along the boundary. Newtonsoft.Json will just request another read with
                // an insufficient buffer size. Instead we need to read the entire surrogate pair and give back the partial
                // surrogate pair.

                Span<char> tempBuffer = stackalloc char[2];

                _decoder.Convert(source.Slice(bytesRead), tempBuffer, false, out var tempBytesRead, out var tempCharsRead,
                    out completed);
                bytesRead += tempBytesRead;

                if (tempCharsRead > 0)
                {
                    destination[converterCharsRead] = tempBuffer[0];
                    charsWritten++;
                }
                if (tempCharsRead == 2)
                {
                    _holdoverCharacter = tempBuffer[1];
                }
            }
#else
            int bytesRead;

            fixed (char* destinationChars = &MemoryMarshal.GetReference(destination))
            {
                fixed (byte* sourceBytes = &MemoryMarshal.GetReference(source))
                {
                    _decoder.Convert(sourceBytes, source.Length, destinationChars, destination.Length, false,
                        out bytesRead, out var converterCharsRead, out var completed);
                    charsWritten += converterCharsRead;

                    if (converterCharsRead < destination.Length && !completed)
                    {
                        // We have encountered a surrogate pair along the boundary. Newtonsoft.Json will just request another read with
                        // an insufficient buffer size. Instead we need to read the entire surrogate pair and give back the partial
                        // surrogate pair.

                        // This is a rare case so we don't retain this small buffer for reuse
                        var tempBuffer = new char[2];

                        fixed (char* tempBufferChars = &tempBuffer[0])
                        {
                            _decoder.Convert(sourceBytes + bytesRead, source.Length - bytesRead, tempBufferChars, tempBuffer.Length, false,
                                out var tempBytesRead, out var tempCharsRead, out completed);
                            bytesRead += tempBytesRead;

                            if (tempCharsRead > 0)
                            {
                                destination[converterCharsRead] = tempBuffer[0];
                                charsWritten++;
                            }

                            if (tempCharsRead == 2)
                            {
                                _holdoverCharacter = tempBuffer[1];
                            }
                        }
                    }
                }
            }
#endif

            return bytesRead;
        }

        public override int Read(char[] buffer, int index, int count)
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

            var destination = buffer.AsSpan(index, count);

            var charsWritten = 0;
            while (charsWritten < count && (!_sequence.IsEmpty || _holdoverCharacter is not null))
            {
#if NET6_0_OR_GREATER
                var nextSpan = _sequence.FirstSpan;
#else
                var nextSpan = _sequence.First.Span;
#endif

                var bytesRead = Read(nextSpan, destination, out var converterCharsWritten);

                _sequence = _sequence.Slice(bytesRead);
                destination = destination.Slice(converterCharsWritten);
                charsWritten += converterCharsWritten;
            }

            return charsWritten;
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
