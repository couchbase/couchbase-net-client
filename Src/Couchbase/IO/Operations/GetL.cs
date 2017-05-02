using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations
{
    internal class GetL<T> : Get<T>
    {
        public GetL(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
        }

        private GetL(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(key, vBucket, transcoder, opaque, timeout)
        {
        }

        public override byte[] CreateExtras()
        {
            var extras = new byte[4];
            Converter.FromUInt32(Expiration, extras, 0);
            return extras;
        }

        public override byte[] Write()
        {
            var key = CreateKey();
            var extras = CreateExtras();
            var header = CreateHeader(extras, new byte[0], key);

            var buffer = new byte[header.GetLengthSafe() + key.GetLengthSafe() + extras.GetLengthSafe()];

            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(extras, 0, buffer, header.Length, extras.Length);
            System.Buffer.BlockCopy(key, 0, buffer, header.Length + extras.Length, key.Length);

            return buffer;
        }

        public uint Expiration { get; set; }

        public override OperationCode OperationCode
        {
            get { return OperationCode.GetL; }
        }

        public override IOperation Clone()
        {
            var cloned = new GetL<T>(Key, VBucket, Transcoder, Opaque, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName
            };
            return cloned;
        }
    }
}
