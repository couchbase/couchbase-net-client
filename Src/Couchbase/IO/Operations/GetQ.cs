﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    internal class GetQ<T>: Get<T>
    {
        public GetQ(string key, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder)
            : base(key, vBucket, converter, transcoder)
        {
        }

        protected GetQ(string key, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder, uint opaque)
            : base(key, vBucket, converter, transcoder, opaque)
        {
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.GetQ; }
        }

        public override IOperation<T> Clone()
        {
            var cloned = new GetQ<T>(Key, VBucket, Converter, Transcoder, Opaque)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime
            };
            return cloned;
        }
    }
}
