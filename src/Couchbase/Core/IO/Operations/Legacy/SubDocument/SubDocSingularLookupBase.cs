using System;
using System.Threading.Tasks;

namespace Couchbase.Core.IO.Operations.Legacy.SubDocument
{
    internal abstract class SubDocSingularLookupBase<T> : SubDocSingularBase<T>
    {
        public override async Task SendAsync(IConnection connection)
        {
            var totalLength = OperationHeader.Length + KeyLength + ExtrasLength + PathLength + BodyLength;
            var buffer = new byte[totalLength];

            WriteHeader(buffer);
            WriteExtras(buffer, OperationHeader.Length);
            WriteKey(buffer, OperationHeader.Length + ExtrasLength);
            WritePath(buffer, OperationHeader.Length + ExtrasLength + KeyLength);
            WriteBody(buffer, OperationHeader.Length + ExtrasLength + KeyLength + BodyLength);

            await connection.SendAsync(buffer, Completed).ConfigureAwait(false);
        }


        public override void WriteHeader(byte[] buffer)
        {
            Converter.FromByte((byte)Magic.Request, buffer, HeaderOffsets.Magic);//0
            Converter.FromByte((byte)OpCode, buffer, HeaderOffsets.Opcode);//1
            Converter.FromInt16(KeyLength, buffer, HeaderOffsets.KeyLength);//2-3
            Converter.FromByte((byte)ExtrasLength, buffer, HeaderOffsets.ExtrasLength);  //4
            //5 datatype?
            if (VBucketId.HasValue)
            {
                Converter.FromInt16(VBucketId.Value, buffer, HeaderOffsets.VBucket);//6-7
            }

            Converter.FromInt32(ExtrasLength + PathLength + KeyLength, buffer, HeaderOffsets.BodyLength);//8-11
            Converter.FromUInt32(Opaque, buffer, HeaderOffsets.Opaque);//12-15
            Converter.FromUInt64(Cas, buffer, HeaderOffsets.Cas);
        }

        public override void WriteExtras(byte[] buffer, int offset)
        {
            Converter.FromInt16(PathLength, buffer, offset); //1-2
            Converter.FromByte((byte) CurrentSpec.PathFlags, buffer, offset + 2); //3

            if (CurrentSpec.DocFlags != SubdocDocFlags.None)
            {
                Converter.FromByte((byte) CurrentSpec.DocFlags, buffer, offset + 3);
            }
        }

        public override void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            // Do nothing, lookups don't return extras
        }

        public override bool CanRetry()
        {
            return ErrorCode == null || ErrorMapRequestsRetry();
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
