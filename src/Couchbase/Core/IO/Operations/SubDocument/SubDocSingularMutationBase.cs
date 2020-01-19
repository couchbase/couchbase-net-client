using System;
using System.Buffers;
using Couchbase.Core.IO.Converters;
using Couchbase.KeyValue;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Operations.SubDocument
{
    internal abstract class SubDocSingularMutationBase<T> : SubDocSingularBase<T>
    {
        public override void WriteExtras(OperationBuilder builder)
        {
            var hasExpiry = Expires > 0;
            var length = CurrentSpec.DocFlags != SubdocDocFlags.None ? 4 : 3;
            if (hasExpiry)
            {
                length += 4;
            }

            Span<byte> buffer = stackalloc byte[length];

            ByteConverter.FromInt16((short) ByteConverter.GetStringByteCount(Path), buffer); //1-2
            buffer[2] = (byte) CurrentSpec.PathFlags; //3

            if (hasExpiry)
            {
                ByteConverter.FromUInt32(Expires, buffer.Slice(3)); //4@27 Expiration time (if present, extras is 7)
            }
            if (CurrentSpec.DocFlags != SubdocDocFlags.None)
            {
                // write doc flags, offset depends on if there is an expiry
                buffer[hasExpiry ? 7 : 3] = (byte) CurrentSpec.DocFlags;
            }

            builder.Write(buffer);
        }

        public override void WriteBody(OperationBuilder builder)
        {
            using (var bufferOwner = MemoryPool<byte>.Shared.Rent(OperationSpec.MaxPathLength))
            {
                var buffer = bufferOwner.Memory.Span;

                var pathLength = ByteConverter.FromString(Path, buffer);

                builder.Write(bufferOwner.Memory.Slice(0, pathLength));
            }

            if (!CurrentSpec.RemoveBrackets)
            {
                // We can serialize directly
                Transcoder.Serializer.Serialize(builder, CurrentSpec.Value);
            }
            else
            {
                using (var stream = MemoryStreamFactory.GetMemoryStream())
                {
                    Transcoder.Serializer.Serialize(stream, CurrentSpec.Value);

                    ReadOnlyMemory<byte> body = stream.GetBuffer().AsMemory(0, (int) stream.Length);
                    body = body.StripBrackets();

                    builder.Write(body);
                }
            }
        }

        public override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            TryReadMutationToken(buffer);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
