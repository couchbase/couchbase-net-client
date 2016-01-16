using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations.SubDocument
{
    internal abstract class SubDocSingularBase<T> : OperationBase<T>
    {
        private short _keyLength;
        private short _pathLength;

        protected SubDocSingularBase(string key, T value, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(key, value, vBucket, transcoder, opaque, timeout)
        {
        }

        protected SubDocSingularBase(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
        }

        public string Path { get; protected set; }

        public override short ExtrasLength
        {
            get
            {
                return (short) (Expires == 0 ? 3 : 7);
            }
        }

        public override short PathLength
        {
            get
            {
                if (_pathLength == 0)
                {
                    _pathLength = (short)Encoding.UTF8.GetByteCount(Path);
                }
                return _pathLength;
            }
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

        public override void WriteBody(byte[] buffer, int offset)
        {
            System.Buffer.BlockCopy(BodyBytes, 0, buffer, offset, BodyLength);
        }

        public override void WriteExtras(byte[] buffer, int offset)
        {
            Converter.FromInt16(PathLength, buffer, offset);
        }

        public override void WriteKey(byte[] buffer, int offset)
        {
            Converter.FromString(Key, buffer, offset);
        }

        public override void WritePath(byte[] buffer, int offset)
        {
            Converter.FromString(Path, buffer, offset);
        }
    }
}
