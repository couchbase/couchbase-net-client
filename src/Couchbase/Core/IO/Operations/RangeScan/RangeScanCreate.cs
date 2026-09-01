using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;

namespace Couchbase.Core.IO.Operations.RangeScan
{
    internal sealed class RangeScanCreate : OperationBase<IScanTypeExt>, IPreMappedVBucketOperation
    {
        /// <inheritdoc />
        // writes no key - the collection travels in the body.
        internal override bool RequiresCollectionId => false;

        //https://github.com/couchbase/kv_engine/blob/master/docs/range_scans/range_scan_create.md

        public override bool RequiresVBucketId => true;

        public override OpCode OpCode => OpCode.RangeScanCreate;

        public bool KeyOnly { private get; set; }

        internal override void WriteExtras(OperationBuilder builder)
        {
            //no extras
        }

        internal override void WriteKey(OperationBuilder builder, bool collectionsEnabled)
        {
            //no key
        }

        protected override bool SupportsJsonDataType => true;

        internal override void WriteBody(OperationBuilder builder)
        {
            Content.CollectionName = Cid?.ToString("x") ?? "";
            Content.Serialize(KeyOnly, Timeout, MutationToken, builder);
        }
    }
}
