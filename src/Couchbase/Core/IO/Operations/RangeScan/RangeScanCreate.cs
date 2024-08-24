using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;

namespace Couchbase.Core.IO.Operations.RangeScan
{
    internal sealed class RangeScanCreate : OperationBase<IScanTypeExt>, IPreMappedVBucketOperation
    {
        //https://github.com/couchbase/kv_engine/blob/master/docs/range_scans/range_scan_create.md

        public override bool RequiresVBucketId => true;

        public override OpCode OpCode => OpCode.RangeScanCreate;

        public bool KeyOnly { private get; set; }

        protected override void WriteExtras(OperationBuilder builder)
        {
            //no extras
        }

        protected override void WriteKey(OperationBuilder builder)
        {
            //no key
        }

        protected override bool SupportsJsonDataType => true;

        protected override void WriteBody(OperationBuilder builder)
        {
            Content.CollectionName = string.Format("{0:x}", Cid);
            var bytes = Content.Serialize(KeyOnly, Timeout, MutationToken);
            builder.Write(bytes);
        }
    }
}
