using System;
using Couchbase.KeyValue;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Operations.RangeScan
{
    internal sealed class RangeScanCancel : OperationBase<SlicedMemoryOwner<byte>>, IPreMappedVBucketOperation
    {
        public override OpCode OpCode => OpCode.RangeScanCancel;

        public override bool RequiresVBucketId => true;

        internal override void WriteBody(OperationBuilder builder)
        {
            //no body
        }

        internal override void WriteKey(OperationBuilder builder)
        {
            //no key
        }

        internal override void WriteExtras(OperationBuilder builder)
        {
            //write uuidbase
            var span = builder.GetSpan(16);
            Content.Memory.Span.CopyTo(span);
            builder.Advance(16);
        }
    }
}
