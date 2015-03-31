using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    internal class Unlock : OperationBase
    {
        public Unlock(IByteConverter converter, uint timeout)
            : base(converter, timeout)
        {
        }

        public Unlock(string key, ITypeTranscoder transcoder, IVBucket vBucket, IByteConverter converter, uint timeout)
            : base(key, vBucket, converter, transcoder, timeout)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Unlock; }
        }

        public override IOperation Clone()
        {
            var cloned = new Unlock(Key, Transcoder, VBucket, Converter, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                Opaque = Opaque
            };
            return cloned;
        }
    }
}
