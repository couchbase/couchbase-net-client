using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    internal class Unlock : OperationBase<string>
    {
        public Unlock(IByteConverter converter)
            : base(converter)
        {
        }

        public Unlock(string key, string value, ITypeTranscoder transcoder, IVBucket vBucket, IByteConverter converter, uint opaque)
            : base(key, value, transcoder, vBucket, converter, opaque)
        {
        }

        public Unlock(string key, string value, IVBucket vBucket, IByteConverter converter)
            : base(key, value, vBucket, converter)
        {
        }

        public Unlock(string key, IVBucket vBucket, IByteConverter converter)
            : base(key, vBucket, converter)
        {
        }

        public Unlock(string key, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder)
            : base(key, vBucket, converter, transcoder)
        {
        }

        public override byte[] CreateExtras()
        {
            return new byte[0];
        }

        public override byte[] CreateBody()
        {
            return new byte[0];
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Unlock; }
        }
    }
}
