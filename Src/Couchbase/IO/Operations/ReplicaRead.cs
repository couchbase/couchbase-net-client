using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Operations
{
    internal class ReplicaRead<T> : OperationBase<T>
    {
        public ReplicaRead(IByteConverter converter, uint timeout)
            : base(converter, timeout)
        {
        }

        private ReplicaRead(string key, ITypeTranscoder transcoder, IVBucket vBucket, IByteConverter converter, uint opaque, uint timeout)
            : base(key, default(T), transcoder, vBucket, converter, opaque, timeout)
        {
        }

        public ReplicaRead(string key, T value, IVBucket vBucket, IByteConverter converter, uint timeout)
            : base(key, value, vBucket, converter, timeout)
        {
        }

        public ReplicaRead(string key, IVBucket vBucket, IByteConverter converter, uint timeout)
            : base(key, vBucket, converter, timeout)
        {
        }

        public ReplicaRead(string key, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, converter, transcoder, timeout)
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
            var cloned = new ReplicaRead<T>(Key, Transcoder, VBucket, Converter, Opaque, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime
            };
            return cloned;
        }
    }
}
