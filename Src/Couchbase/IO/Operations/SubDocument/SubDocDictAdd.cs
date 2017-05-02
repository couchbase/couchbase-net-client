using Couchbase.Core;
using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations.SubDocument
{
    internal class SubDocDictAdd<T> : SubDocSingularMutationBase<T>
    {
        public SubDocDictAdd(MutateInBuilder<T> builder, string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(builder, key, vBucket, transcoder, SequenceGenerator.GetNext(), timeout)
        {
            CurrentSpec = builder.FirstSpec();
            Path = CurrentSpec.Path;
            Cas = builder.Cas;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SubDictAdd; }
        }

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns></returns>
        public override IOperation Clone()
        {
            return new SubDocDictAdd<T>((MutateInBuilder<T>)((MutateInBuilder<T>)Builder).Clone(), Key, VBucket, Transcoder, Timeout)
            {
                Attempts = Attempts,
                Cas = Cas,
                CreationTime = CreationTime,
                LastConfigRevisionTried = LastConfigRevisionTried,
                BucketName = BucketName
            };
        }
    }
}
