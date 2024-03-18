using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Transcoders
{
    public class RawJsonTranscoder : BaseTranscoder
    {
        private const int BufferSize = 1024;

        public override Flags GetFormat<T>(T value)
        {
            var typeCode = Type.GetTypeCode(typeof(T));
            if (typeof(T) == typeof(byte[]) ||
                typeof(T) == typeof(Memory<byte>) ||
                typeof(T) == typeof(ReadOnlyMemory<byte>) ||
                typeof(T) == typeof(string))
            {
                var dataFormat = DataFormat.Json;
                return new Flags { Compression = Operations.Compression.None, DataFormat = dataFormat, TypeCode = typeCode };
            }

            ThrowHelper.ThrowInvalidOperationException("The RawJsonTranscoder only supports byte arrays as input.");
            return default; // unreachable
        }

        public override void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode)
        {
            // For value types this typeof check approach allows eliding branches during JIT
            if (typeof(T) == typeof(Memory<byte>))
            {
                stream.Write((Memory<byte>)(object)value!);
                return;
            }
            if (typeof(T) == typeof(ReadOnlyMemory<byte>))
            {
                stream.Write((ReadOnlyMemory<byte>)(object)value!);
                return;
            }

            if (value is byte[] bytes)
            {
                stream.Write(bytes, 0, bytes.Length);
                return;
            }

            if (value is string strValue)
            {
                if (strValue.Length <= BufferSize)
                {
                    // For small strings (less than the buffer size), it is more efficient to avoid the cost of allocating the buffers
                    // within a StreamWriter and serialize directly to a pooled buffer instead.

                    var buffer = ArrayPool<byte>.Shared.Rent(ByteConverter.GetStringByteCount(strValue));
                    try
                    {
                        var length = ByteConverter.FromString(strValue, buffer.AsSpan());
                        stream.Write(buffer, 0, length);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                else
                {
                    // For larger strings, use a StreamWriter to serialize the stream in blocks to avoid allocating a very large buffer.

                    using var writer = new StreamWriter(stream, EncodingUtils.Utf8NoBomEncoding, BufferSize, leaveOpen: true);
                    writer.Write(strValue);
                }

                return;
            }

            ThrowHelper.ThrowInvalidOperationException("The RawJsonTranscoder can only encode JSON byte arrays.");
        }

        [return: MaybeNull]
        public override T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode)
        {
            var targetType = typeof(T);
            if (targetType == typeof(byte[]))
            {
                object value = DecodeBinary(buffer.Span);
                return (T)value;
            }

            if (typeof(T) == typeof(IMemoryOwner<byte>))
            {
                // Note: it is important for the consumer to dispose of the returned IMemoryOwner<byte>, in keeping
                // with IMemoryOwner<T> conventions. Failure to properly dispose this object will result in the memory
                // not being returned to the pool, which will increase GC impact across various parts of the framework.

#if NET6_0_OR_GREATER
                var memoryOwner = MemoryPool<byte>.Shared.RentAndSlice(buffer.Length);
#else
                var memoryOwner = OperationResponseMemoryPool.Instance.RentAndSlice(buffer.Length);
#endif
                try
                {
                    buffer.CopyTo(memoryOwner.Memory);

                    // This boxes the SlicedMemoryOwner on the heap, making it act like a class to the consumer
                    return (T)(object)memoryOwner;
                }
                catch
                {
                    // Cleanup if the copy fails
                    memoryOwner.Dispose();
                    throw;
                }
            }

            if (targetType == typeof(string))
            {
                object? value = DecodeString(buffer.Span);
                return (T?) value;
            }

            ThrowHelper.ThrowInvalidOperationException("The RawJsonTranscoder can only decode JSON byte arrays.");
            return default!; // unreachable
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
