using Couchbase.Core;
using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations.SubDocument
{
    internal class SubExists<T> : SubDocSingularLookupBase<T>
    {
        public SubExists(LookupInBuilder<T> builder, string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(builder, key, vBucket, transcoder, timeout)
        {
            CurrentSpec = builder.FirstSpec();
            Path = CurrentSpec.Path;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SubExist; }
        }

        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns></returns>
        public override IOperation Clone()
        {
            return new SubExists<T>((LookupInBuilder<T>)((LookupInBuilder<T>)Builder).Clone(), Key, VBucket, Transcoder, Timeout)
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
