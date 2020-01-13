using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Couchbase.Utils
{
    internal static class StreamExtensions
    {
#if NETCOREAPP2_1 || NETSTANDARD2_1
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(this Stream stream, ReadOnlyMemory<byte> buffer)
        {
            stream.Write(buffer.Span);
        }
#else
        public static void Write(this Stream stream, ReadOnlyMemory<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            if (MemoryMarshal.TryGetArray(buffer, out var segment))
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                stream.Write(segment.Array, segment.Offset, segment.Count);
            }
            else
            {
                var byteBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
                try
                {
                    buffer.CopyTo(byteBuffer);
                    stream.Write(byteBuffer, 0, buffer.Length);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(byteBuffer);
                }
            }
        }
#endif
    }
}
