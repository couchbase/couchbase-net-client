using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    internal class GetK<T> : Get<T>
    {
        public GetK(string key, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder)
            : base(key, vBucket, converter, transcoder)
        {
        }

        protected GetK(string key, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder, uint opaque)
            : base(key, vBucket, converter, transcoder, opaque)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.GetK; }
        }

        public override IOperation<T> Clone()
        {
            var cloned = new GetK<T>(Key, VBucket, Converter, Transcoder, Opaque)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime
            };
            return cloned;
        }
    }
}
