using System;
using Couchbase.Core.IO.Converters;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Operations.Legacy.SubDocument
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

            Converter.FromInt16((short) Converter.GetStringByteCount(Path), buffer); //1-2
            Converter.FromByte((byte) CurrentSpec.PathFlags, buffer.Slice(2)); //3

            if (hasExpiry)
            {
                Converter.FromUInt32(Expires, buffer.Slice(3)); //4@27 Expiration time (if present, extras is 7)
            }
            if (CurrentSpec.DocFlags != SubdocDocFlags.None)
            {
                // write doc flags, offset depends on if there is an expiry
                Converter.FromByte((byte) CurrentSpec.DocFlags, buffer.Slice(hasExpiry ? 7 : 3));
            }

            builder.Write(buffer);
        }

        public override byte[] CreateBody()
        {
            // TODO: This is inefficient, we'll optimize with streams
            var pathLength = Converter.GetStringByteCount(Path);
            var body = Transcoder.Serializer.Serialize(CurrentSpec.Value);
            if (CurrentSpec.RemoveBrackets)
            {
                body = body.StripBrackets();
            }

            var buffer = new byte[pathLength + body.Length];
            Converter.FromString(Path, buffer);
            Buffer.BlockCopy(body, 0, buffer, pathLength, body.Length);
            return buffer;
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
