using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations.SubDocument
{
    internal abstract class SubDocSingularLookupBase<T> : SubDocSingularBase<T>
    {
        protected SubDocSingularLookupBase(ISubDocBuilder<T> builder, string key, T value, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(builder, key, value, vBucket, transcoder, opaque, timeout)
        {
        }

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
            Converter.FromInt16(PathLength, buffer, offset);
        }
    }
}
