using System;
using System.Buffers;
using System.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Transcoders
{
    public class RawBinaryTranscoder : BaseTranscoder
    {
        public override Flags GetFormat<T>(T value)
        {
            var typeCode = Type.GetTypeCode(typeof(T));
            if (typeof(T) == typeof(byte[]) || typeof(T) == typeof(Memory<byte>) || typeof(T) == typeof(ReadOnlyMemory<byte>))
            {
                var dataFormat = DataFormat.Binary;
                return new Flags { Compression = Operations.Compression.None, DataFormat = dataFormat, TypeCode = typeCode };
            }

            throw new InvalidOperationException("The RawBinaryTranscoder only supports byte arrays, Memory<byte>, and ReadOnlyMemory<byte> as input.");
        }

        public override void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode)
        {
            if(value is byte[] bytes)
            {
                stream.Write(bytes, 0, bytes.Length);
                return;
            }
            if (value is Memory<byte> memory)
            {
                stream.Write(memory);
                return;
            }
            if (value is ReadOnlyMemory<byte> readOnlyMemory)
            {
                stream.Write(readOnlyMemory);
                return;
            }

            throw new InvalidOperationException("The RawBinaryTranscoder can only encode byte arrays, Memory<byte>, and ReadOnlyMemory<byte>.");
        }

        public override T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode)
        {
            if (typeof(T) == typeof(byte[]))
            {
                object value = DecodeBinary(buffer.Span);
                return (T) value;
            }

            if (typeof(T) == typeof(IMemoryOwner<byte>))
            {
                // Note: it is important for the consumer to dispose of the returned IMemoryOwner<byte>, in keeping
                // with IMemoryOwner<T> conventions. Failure to properly dispose this object will result in the memory
                // not being returned to the pool, which will increase GC impact across various parts of the framework.

                var memoryOwner = MemoryPool<byte>.Shared.RentAndSlice(buffer.Length);
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

            throw new InvalidOperationException("The RawBinaryTranscoder can only decode byte arrays or IMemoryOwner<byte>.");
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
