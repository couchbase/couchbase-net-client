using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations.Authentication
{
    internal sealed class SelectBucket : OperationBase
    {
        public SelectBucket(string key, ITypeTranscoder transcoder, uint timeout)
            : base(key, null, transcoder, timeout)
        { }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SelectBucket; }
        }

        public override bool RequiresKey
        {
            get { return true; }
        }
    }
}
