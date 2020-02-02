using System.Buffers;
using Couchbase.Core.IO.Transcoders;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.KeyValue
{
    internal class GetReplicaResult : GetResult, IGetReplicaResult
    {
        public bool IsActive { get; internal set; }

        public GetReplicaResult(IMemoryOwner<byte> contentBytes, ITypeTranscoder transcoder,
            ILogger<GetResult> logger)
            : base(contentBytes, transcoder, logger)
        { }
    }
}
