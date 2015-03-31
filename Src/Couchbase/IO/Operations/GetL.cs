using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    internal class GetL<T> : Get<T>
    {
        public GetL(string key, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, converter, transcoder, timeout)
        {
        }

        private GetL(string key, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(key, vBucket, converter, transcoder, opaque, timeout)
        {
        }

        public override byte[] CreateExtras()
        {
            var extras = new byte[4];
            Converter.FromUInt32(Expiration, extras, 0);
            return extras;
        }

        public uint Expiration { get; set; }

        public override OperationCode OperationCode
        {
            get { return OperationCode.GetL; }
        }

        public override IOperation Clone()
        {
            var cloned = new GetL<T>(Key, VBucket, Converter, Transcoder, Opaque, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime
            };
            return cloned;
        }
    }
}
