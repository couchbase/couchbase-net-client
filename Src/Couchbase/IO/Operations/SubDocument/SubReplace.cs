using Couchbase.Core;
using Couchbase.Core.Transcoders;

namespace Couchbase.IO.Operations.SubDocument
{
    internal class SubReplace<T> : SubDocSingularBase<T>
    {
        public SubReplace(string key, string path, T value, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
            Path = path;
            _value = value;
        }

        public override OperationCode OperationCode
        {
            get { return OperationCode.SubReplace; }
        }
    }
}
