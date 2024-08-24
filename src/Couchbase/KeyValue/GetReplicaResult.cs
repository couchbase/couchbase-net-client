using System.Buffers;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.KeyValue
{
    internal sealed class GetReplicaResult : GetResult, IGetReplicaResult
    {
        public bool IsActive { get; internal set; }

        public GetReplicaResult(in SlicedMemoryOwner<byte> contentBytes, ITypeTranscoder transcoder,
            ILogger<GetResult> logger, IFallbackTypeSerializerProvider fallbackTypeSerializerProvider)
            : base(in contentBytes, transcoder, logger, fallbackTypeSerializerProvider)
        { }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
