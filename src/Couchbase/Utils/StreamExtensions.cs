using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Couchbase.Utils
{
    internal static class StreamExtensions
    {
#if SPAN_SUPPORT
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

            if (stream is IBufferWriter<byte> bufferWriter)
            {
                // OperationBuilder implements IBufferWriter<byte> which can be used to write directly buffer
                bufferWriter.Write(buffer.Span);
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
