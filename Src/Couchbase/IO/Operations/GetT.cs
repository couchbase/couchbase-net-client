using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations
{
    internal class GetT<T> : OperationBase<T>
    {
        public GetT(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
        }

        protected GetT(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(key, default(T), vBucket, transcoder, opaque, timeout)
        {
        }

        public override byte[] CreateExtras()
        {
            var extras = new byte[4];
            Converter.FromUInt32(Expires, extras, 0);
            return extras;
        }

        public override byte[] Write()
        {
            var key = CreateKey();
            var extras = CreateExtras();
            var body = new byte[0];
            var header = CreateHeader(extras, body, key);

            var buffer = new byte[header.GetLengthSafe() + key.GetLengthSafe() + extras.GetLengthSafe()];

            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(extras, 0, buffer, header.Length, extras.Length);
            System.Buffer.BlockCopy(key, 0, buffer, header.Length + extras.Length, key.Length);

            return buffer;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.GAT; }
        }

        public override IOperation Clone()
        {
            var cloned = new GetT<T>(Key, VBucket, Transcoder, Opaque, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                Expires = Expires,
                LastConfigRevisionTried = LastConfigRevisionTried
            };
            return cloned;
        }

        public override bool RequiresKey
        {
            get { return true; }
        }
    }
}
