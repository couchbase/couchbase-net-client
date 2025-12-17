using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Transcoders
{
    public class RawStringTranscoder : BaseTranscoder
    {
        private const int BufferSize = 1024;

        [RequiresUnreferencedCode(DefaultSerializer.UnreferencedCodeMessage)]
        [RequiresDynamicCode(DefaultSerializer.RequiresDynamicCodeMessage)]
        public RawStringTranscoder() : this(DefaultSerializer.Instance)
        {
        }

        public RawStringTranscoder(ITypeSerializer serializer)
        {
            Serializer = serializer;
        }

        public override Flags GetFormat<T>(T value)
        {
            if (typeof(T) == typeof(string))
            {
                return new Flags
                {
                    Compression = Operations.Compression.None,
                    DataFormat = DataFormat.String,
                    TypeCode = TypeCode.String
                };
            }

            ThrowHelper.ThrowInvalidOperationException("The RawStringTranscoder only supports strings as input.");
            return default; // unreachable
        }

        public override void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode)
        {
            if (value is string str)
            {
                if (stream is IBufferWriter<byte> bufferWriter)
                {
                    // OperationBuilder implements IBufferWriter<byte> which can be used to write directly buffer
                    bufferWriter.WriteUtf8String(str);
                }
                else if (str.Length <= BufferSize)
                {
                    // For small strings (less than the buffer size), it is more efficient to avoid the cost of allocating the buffers
                    // within a StreamWriter and serialize directly to a pooled buffer instead.

                    var buffer = ArrayPool<byte>.Shared.Rent(ByteConverter.GetStringByteCount(str));
                    try
                    {
                        var length = ByteConverter.FromString(str, buffer.AsSpan());
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
                    writer.Write(str);
                }

                return;
            }

            ThrowHelper.ThrowInvalidOperationException("The RawStringTranscoder can only encode strings.");
        }

        [return: MaybeNull]
        public override T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode)
        {
            var type = typeof(T);
            if (type == typeof(string))
            {
                object? value = DecodeString(buffer.Span);
                return (T?) value;
            }

            ThrowHelper.ThrowInvalidOperationException("The RawStringTranscoder can only decode strings.");
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
