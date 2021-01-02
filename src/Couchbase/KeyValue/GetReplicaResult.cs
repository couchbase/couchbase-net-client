using System.Buffers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.KeyValue
{
    internal class GetReplicaResult : GetResult, IGetReplicaResult
    {
        public bool IsActive { get; internal set; }

        public GetReplicaResult(in SlicedMemoryOwner<byte> contentBytes, ITypeTranscoder transcoder,
            ILogger<GetResult> logger)
            : base(in contentBytes, transcoder, logger)
        { }
    }
}
