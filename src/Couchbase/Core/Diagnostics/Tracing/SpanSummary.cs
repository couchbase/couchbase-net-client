using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Utils;
using Newtonsoft.Json;
using Tags = Couchbase.Core.Diagnostics.Tracing.CouchbaseTags.OpenTracingTags;

namespace Couchbase.Core.Diagnostics.Tracing
{
    internal class SpanSummary : IComparable<SpanSummary>
    {
        [JsonIgnore]
        public string ServiceType { get; set; }

        [JsonProperty("operation_name")]
        public string OperationName { get; set; }

        [JsonProperty("last_operaion_id", NullValueHandling = NullValueHandling.Ignore)]
        public string LastOperationId { get; set; }

        [JsonProperty("last_local_address", NullValueHandling = NullValueHandling.Ignore)]
        public string LastLocalAddress { get; set; }

        [JsonProperty("last_remote_address", NullValueHandling = NullValueHandling.Ignore)]
        public string LastRemoteAddress { get; set; }

        [JsonProperty("last_local_id", NullValueHandling = NullValueHandling.Ignore)]
        public string LastLocalId { get; set; }

        [JsonProperty("last_dispatch_us", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public long LastDispatchDuration { get; set; }

        [JsonProperty("total_us")]
        public long TotalDuration { get; set; }

        [JsonProperty("encode_us", NullValueHandling = NullValueHandling.Ignore)]
        public long EncodingDuration { get; set; }

        [JsonProperty("dispatch_us", NullValueHandling = NullValueHandling.Ignore)]
        public long DispatchDuration { get; set; }

        [JsonProperty("server_us", NullValueHandling = NullValueHandling.Ignore)]
        public long? ServerDuration { get; set; }

        [JsonProperty("decode_us", NullValueHandling = NullValueHandling.Ignore)]
        public long DecodingDuration { get; set; }

        internal SpanSummary()
        {

        }

        internal SpanSummary(Span span)
        {
            ServiceType = span.Tags.TryGetValue(CouchbaseTags.Service, out var serviceName)
                ? (string) serviceName
                : string.Empty;
            OperationName = span.OperationName;
            TotalDuration = span.Duration;
            TrySetOpeationId(span);
            PopulateSummary(span.Spans);
        }

        private void PopulateSummary(IEnumerable<Span> spans)
        {
            foreach (var span in spans.ToList())
            {
                TrySetOpeationId(span);

                switch (span.OperationName)
                {
                    case CouchbaseOperationNames.RequestEncoding:
                        EncodingDuration += span.Duration;
                        break;
                    case CouchbaseOperationNames.DispatchToServer:
                        DispatchDuration += span.Duration;
                        LastDispatchDuration = span.Duration;

                        if (span.Tags.TryGetValue(CouchbaseTags.LocalAddress, out var local))
                        {
                            LastLocalAddress = local.ToString();
                        }

                        if (span.Tags.TryGetValue(Tags.PeerHostIpv4, out var remote))
                        {
                            LastRemoteAddress = remote.ToString();
                        }

                        if (span.Tags.TryGetValue(CouchbaseTags.LocalId, out var localId))
                        {
                            LastLocalId = localId.ToString();
                        }

                        if (span.Tags.TryGetValue(CouchbaseTags.PeerLatency, out var duration))
                        {
                            if (TimeSpanExtensions.TryConvertToMicros(duration, out var value))
                            {
                                if (ServerDuration.HasValue)
                                {
                                    ServerDuration += value;
                                }
                                else
                                {
                                    ServerDuration = value;
                                }
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

        private void TrySetOpeationId(Span span)
        {
            if (span.Tags.TryGetValue(CouchbaseTags.OperationId, out var id))
            {
                LastOperationId = id.ToString();
            }
        }

        public int CompareTo(SpanSummary other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return Nullable.Compare(other.ServerDuration, ServerDuration);
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
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
