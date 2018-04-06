using System.Collections.Generic;
using Newtonsoft.Json;
using OpenTracing;

namespace Couchbase.Tracing
{
    internal class SpanSummary
    {
        [JsonProperty("operation_name")]
        public string OperationName { get; set; }

        [JsonProperty("last_operaion_id", NullValueHandling = NullValueHandling.Ignore)]
        public string OperationId { get; set; }

        [JsonProperty("last_local_address", NullValueHandling = NullValueHandling.Ignore)]
        public string LastLocalAddress { get; set; }

        [JsonProperty("last_remote_address", NullValueHandling = NullValueHandling.Ignore)]
        public string LastRemoteAddress { get; set; }

        [JsonProperty("last_local_id", NullValueHandling = NullValueHandling.Ignore)]
        public string LastLocalId { get; set; }

        [JsonProperty("total_duration_us")]
        public long TotalDuration { get; set; }

        [JsonProperty("encode_us", NullValueHandling = NullValueHandling.Ignore)]
        public long EncodingDuration { get; set; }

        [JsonProperty("dispatch_us", NullValueHandling = NullValueHandling.Ignore)]
        public long DispatchDuration { get; set; }

        [JsonProperty("decode_us", NullValueHandling = NullValueHandling.Ignore)]
        public long DecodingDuration { get; set; }

        [JsonProperty("server_duration_us", NullValueHandling = NullValueHandling.Ignore)]
        public long? ServerDuration { get; set; }

        public SpanSummary(Span span)
        {
            OperationName = span.OperationName;
            TotalDuration = span.Duration;
            PopulateSummary(span.Spans);
        }

        private void PopulateSummary(IEnumerable<Span> spans)
        {
            foreach (var span in spans)
            {
                if (span.Tags.TryGetValue(CouchbaseTags.OperationId, out var id))
                {
                    OperationId = id.ToString();
                }

                switch (span.OperationName)
                {
                    case CouchbaseOperationNames.RequestEncoding:
                        EncodingDuration += span.Duration;
                        break;
                    case CouchbaseOperationNames.DispatchToServer:
                        DispatchDuration += span.Duration;

                        if (span.Tags.TryGetValue(CouchbaseTags.LocalAddress, out var local))
                        {
                            LastLocalAddress = local.ToString();
                        }

                        if (span.Tags.TryGetValue(Tags.PeerAddress, out var remote))
                        {
                            LastRemoteAddress = remote.ToString();
                        }

                        if (span.Tags.TryGetValue(CouchbaseTags.LocalId, out var localId))
                        {
                            LastLocalId = localId.ToString();
                        }

                        if (span.Tags.TryGetValue(CouchbaseTags.PeerLatency, out var duration))
                        {
                            var value = long.Parse(duration.ToString());
                            if (ServerDuration.HasValue)
                            {
                                ServerDuration += value;
                            }
                            else
                            {
                                ServerDuration = value;
                            }
                        }
                        break;
                    case CouchbaseOperationNames.ResponseDecoding:
                        DecodingDuration += span.Duration;
                        break;
                }

                PopulateSummary(span.Spans);
            }
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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

#endregion
