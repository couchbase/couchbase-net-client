using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations.SubDocument
{
    internal abstract class SubDocSingularLookupBase<T> : SubDocSingularBase<T>
    {
        protected SubDocSingularLookupBase(ISubDocBuilder<T> builder, string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(builder, key, vBucket, transcoder, timeout)
        {
        }

        public override byte[] Write()
        {
            var totalLength = HeaderLength + KeyLength + ExtrasLength + PathLength + BodyLength;
            var buffer = AllocateBuffer(totalLength);

            WriteHeader(buffer);
            WriteExtras(buffer, HeaderLength);
            WriteKey(buffer, HeaderLength + ExtrasLength);
            WritePath(buffer, HeaderLength + ExtrasLength + KeyLength);
            WriteBody(buffer, HeaderLength + ExtrasLength + KeyLength + BodyLength);

            return buffer;
        }

        public override byte[] AllocateBuffer(int length)
        {
            return new byte[length];
        }

        public override void WriteHeader(byte[] buffer)
        {
            Converter.FromByte((byte)Magic.Request, buffer, HeaderIndexFor.Magic);//0
            Converter.FromByte((byte)OperationCode, buffer, HeaderIndexFor.Opcode);//1
            Converter.FromInt16(KeyLength, buffer, HeaderIndexFor.KeyLength);//2-3
            Converter.FromByte((byte)ExtrasLength, buffer, HeaderIndexFor.ExtrasLength);  //4
            //5 datatype?
            if (VBucket != null)
            {
                Converter.FromInt16((short)VBucket.Index, buffer, HeaderIndexFor.VBucket);//6-7
            }

            Converter.FromInt32(ExtrasLength + PathLength + KeyLength, buffer, HeaderIndexFor.BodyLength);//8-11
            Converter.FromUInt32(Opaque, buffer, HeaderIndexFor.Opaque);//12-15
            Converter.FromUInt64(Cas, buffer, HeaderIndexFor.Cas);
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

        public override void ReadExtras(byte[] buffer)
        {
            // Do nothing, lookups don't return extras
        }

        public override bool CanRetry()
        {
            return true;
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
