using Couchbase.Core;
using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations
{
    internal class Noop : OperationBase
    {
        public Noop(ITypeTranscoder transcoder, uint timeout)
            : this(string.Empty, null, transcoder, timeout)
        {
        }
        public Noop(string key, ITypeTranscoder transcoder, IVBucket vBucket, uint opaque, uint timeout)
            : base(key, vBucket, transcoder, opaque, timeout)
        {
        }

        public Noop(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.NoOp; }
        }

        public override byte[] Write()
        {
            return CreateHeader(new byte[0], new byte[0], null);
        }
    }
}
