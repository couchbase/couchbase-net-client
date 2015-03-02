using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    internal class Unlock : OperationBase<string>
    {
        public Unlock(IByteConverter converter, uint timeout)
            : base(converter, timeout)
        {
        }

        private Unlock(string key, ITypeTranscoder transcoder, IVBucket vBucket, IByteConverter converter, uint opaque, uint timeout)
            : base(key, default(string), transcoder, vBucket, converter, opaque, timeout)
        {
        }

        public Unlock(string key, string value, IVBucket vBucket, IByteConverter converter, uint timeout)
            : base(key, value, vBucket, converter, timeout)
        {
        }

        public Unlock(string key, IVBucket vBucket, IByteConverter converter, uint timeout)
            : base(key, vBucket, converter, timeout)
        {
        }

        public Unlock(string key, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, converter, transcoder, timeout)
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

        public override IOperation<string> Clone()
        {
            var cloned = new Unlock(Key, Transcoder, VBucket, Converter, Opaque, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime
            };
            return cloned;
        }
    }
}
