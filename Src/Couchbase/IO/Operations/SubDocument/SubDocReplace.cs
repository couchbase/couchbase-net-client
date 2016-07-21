using Couchbase.Core;
using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations.SubDocument
{
    internal class SubDocReplace<T> : SubDocSingularMutationBase<T>
    {
        public SubDocReplace(MutateInBuilder<T> builder, string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(builder, key, vBucket, transcoder, SequenceGenerator.GetNext(), timeout)
        {
            CurrentSpec = builder.FirstSpec();
            Path = CurrentSpec.Path;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SubReplace; }
        }
    }
}
