using System;
using System.IO;
using Couchbase.Core.IO.Operations;

namespace Couchbase.Core.IO.Transcoders
{
    public class RawBinaryTranscoder : BaseTranscoder
    {
        public override Flags GetFormat<T>(T value)
        {
            var typeCode = Type.GetTypeCode(typeof(T));
            if (typeof(T) == typeof(byte[]))
            {
                var dataFormat = DataFormat.Binary;
                return new Flags { Compression = Operations.Compression.None, DataFormat = dataFormat, TypeCode = typeCode };
            }

            throw new InvalidOperationException("The RawBinaryTranscoder only supports byte arrays as input.");
        }

        public override void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode)
        {
            if(value is byte[] bytes)
            {
                stream.Write(bytes, 0, bytes.Length);
                return;
            }

            throw new InvalidOperationException("The RawBinaryTranscoder can only encode byte arrays.");
        }

        public override T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode)
        {
            if (typeof(T) == typeof(byte[]))
            {
                object value = DecodeBinary(buffer.Span);
                return (T) value;
            }

            throw new InvalidOperationException("The RawBinaryTranscoder can only decode byte arrays.");
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
