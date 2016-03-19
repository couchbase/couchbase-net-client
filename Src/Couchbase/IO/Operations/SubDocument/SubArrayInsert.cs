using Couchbase.Core;
using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations.SubDocument
{
    internal class SubArrayInsert<T> : SubDocSingularMutationBase<T>
    {
        public SubArrayInsert(MutateInBuilder<T> builder, string key, T value, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(builder, key, value, vBucket, transcoder, SequenceGenerator.GetNext(), timeout)
        {
            CurrentSpec = builder.FirstSpec();
            Path = CurrentSpec.Path;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SubArrayInsert; }
        }
    }
}
