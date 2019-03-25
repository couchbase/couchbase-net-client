using Couchbase.Core;
using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations.SubDocument
{
    internal class SubDocUpsert<T> : SubDocSingularMutationBase<T>
    {
        public override OperationCode OperationCode => OperationCode.Set;
        public override bool RequiresKey => false;

        public SubDocUpsert(MutateInBuilder<T> builder, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(builder, string.Empty, vBucket, transcoder, SequenceGenerator.GetNext(), timeout)
        {
            CurrentSpec = builder.FirstSpec();
            Cas = builder.Cas;
        }

        public override IOperation Clone()
        {
            return new SubDocUpsert<T>((MutateInBuilder<T>)((MutateInBuilder<T>)Builder).Clone(), VBucket, Transcoder, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName,
                ErrorCode = ErrorCode,
                Expires = Expires
            };
        }
    }
}
