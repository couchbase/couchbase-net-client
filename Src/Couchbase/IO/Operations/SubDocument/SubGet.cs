using Couchbase.Core;
using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations.SubDocument
{
    internal class SubGet<T> : SubDocSingularBase<T>
    {
        public SubGet(string key, string path, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
            Path = path;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SubGet; }
        }
    }
}
