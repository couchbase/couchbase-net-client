using System.Buffers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;

namespace Couchbase
{
    internal class GetReplicaResult : GetResult, IGetReplicaResult
    {
        public bool IsActive { get; internal set; }

        public GetReplicaResult(IMemoryOwner<byte> contentBytes, ITypeTranscoder transcoder)
            : base(contentBytes, transcoder)
        { }
    }
}
