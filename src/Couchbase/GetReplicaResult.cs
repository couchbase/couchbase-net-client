using System.Buffers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Services.KeyValue;

namespace Couchbase
{
    internal class GetReplicaResult : GetResult, IGetReplicaResult
    {
        public bool IsMaster { get; internal set; }

        public GetReplicaResult(IMemoryOwner<byte> contentBytes, ITypeTranscoder transcoder)
            : base(contentBytes, transcoder)
        { }
    }
}
