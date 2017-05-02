using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    internal class Unlock : OperationBase
    {
        public Unlock(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Unlock; }
        }

        public override IOperation Clone()
        {
            var cloned = new Unlock(Key, VBucket, Transcoder, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                Opaque = Opaque,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName
            };
            return cloned;
        }

        public override bool RequiresKey
        {
            get { return true; }
        }
    }
}
