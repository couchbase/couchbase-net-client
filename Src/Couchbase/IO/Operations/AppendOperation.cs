using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Serializers;
using Couchbase.IO.Converters;

namespace Couchbase.IO.Operations
{
    internal sealed class AppendOperation<T> : OperationBase<T>
    {
        public AppendOperation(IByteConverter converter) : base(converter)
        {
        }

        public AppendOperation(string key, T value, ITypeSerializer serializer, IVBucket vBucket, IByteConverter converter) : base(key, value, serializer, vBucket, converter)
        {
        }

        public AppendOperation(string key, T value, IVBucket vBucket, IByteConverter converter) : base(key, value, vBucket, converter)
        {
        }

        public AppendOperation(string key, IVBucket vBucket, IByteConverter converter) : base(key, vBucket, converter)
        {
        }

        public AppendOperation(string key, IVBucket vBucket, IByteConverter converter, ITypeSerializer serializer) : base(key, vBucket, converter, serializer)
        {
        }

        public override byte[] CreateExtras()
        {
            return new byte[0];
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.Append; }
        }
    }
}
