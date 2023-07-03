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

            if (_memory.Length == 0 || count == 0)
            {
                return 0;
            }

            var span = _memory.Span;

#if SPAN_SUPPORT
            var destination = buffer.AsSpan(index, count);
            _decoder.Convert(span, destination, false, out var bytesRead, out var charsRead, out _);
#else
            int bytesRead;
            int charsRead;

            fixed (char* destinationChars = &buffer[index])
            {
                fixed (byte* sourceBytes = &MemoryMarshal.GetReference(span))
                {
                    _decoder.Convert(sourceBytes, span.Length, destinationChars, count, false,
                        out bytesRead, out charsRead, out _);
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
