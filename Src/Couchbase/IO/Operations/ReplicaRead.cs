using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations
{
    internal class ReplicaRead<T> : OperationBase<T>
    {
        private ReplicaRead(string key, ITypeTranscoder transcoder, IVBucket vBucket, uint opaque, uint timeout)
            : base(key, default(T), vBucket, transcoder, opaque, timeout)
        {
        }

        public ReplicaRead(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.ReplicaRead; }
        }

        public override byte[] CreateExtras()
        {
            return new byte[0];
        }

        public override byte[] Write()
        {
            var key = CreateKey();
            var header = CreateHeader(new byte[0], new byte[0], key);

            var buffer = new byte[key.GetLengthSafe() + header.GetLengthSafe()];

            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(key, 0, buffer, header.Length, key.Length);

            return buffer;
        }

        public override int BodyOffset
        {
            get { return 28; }
        }

        public override IOperation Clone()
        {
            var cloned = new ReplicaRead<T>(Key, Transcoder, VBucket, Opaque, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime
            };
            return cloned;
        }
    }
}
