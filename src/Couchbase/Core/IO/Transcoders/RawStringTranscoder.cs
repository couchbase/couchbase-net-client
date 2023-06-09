using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;

#nullable enable

namespace Couchbase.Core.IO.Transcoders
{
    public class RawStringTranscoder : BaseTranscoder
    {
        [RequiresUnreferencedCode(DefaultSerializer.UnreferencedCodeMessage)]
        public RawStringTranscoder() : this(DefaultSerializer.Instance)
        {
        }

        public RawStringTranscoder(ITypeSerializer serializer)
        {
            Serializer = serializer;
        }

        public override Flags GetFormat<T>(T value)
        {
            var typeCode = Type.GetTypeCode(typeof(T));
            if (typeCode == TypeCode.String)
            {
                var dataFormat = DataFormat.String;
                return new Flags {Compression = Operations.Compression.None, DataFormat = dataFormat, TypeCode = typeCode};
            }

            throw new InvalidOperationException("The RawStringTranscoder only supports strings as input.");
        }

        public override void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode)
        {
            if (value is byte[] bytes)
            {
                stream.Write(bytes, 0, bytes.Length);
                return;
            }
            if (value is string str)
            {
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

                return;
            }

            throw new InvalidOperationException("The RawStringTranscoder can only encode strings.");
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

            throw new InvalidOperationException("The RawStringTranscoder can only decode strings.");
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
