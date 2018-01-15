using System.Collections.Generic;
using Newtonsoft.Json;
using OpenTracing;

namespace Couchbase.Tracing
{
    internal class SpanSummary
    {
        [JsonProperty("operaion_id")]
        public string OperationId { get; set; }

        [JsonProperty("local", NullValueHandling = NullValueHandling.Ignore)]
        public string LocalEndpoint { get; set; }

        [JsonProperty("remote", NullValueHandling = NullValueHandling.Ignore)]
        public string RemoteEndpoint { get; set; }

        [JsonProperty("total_duration_us")]
        public long TotalDuration { get; set; }

        [JsonProperty("encode_us")]
        public long EncodingDuration { get; set; }

        [JsonProperty("dispatch_us")]
        public long DispatchDuration { get; set; }

        [JsonProperty("decode_us")]
        public long DecodingDuration { get; set; }

        [JsonProperty("server_duration_us", NullValueHandling = NullValueHandling.Ignore)]
        public long? ServerDuration { get; set; }

        public SpanSummary(Span span)
        {
            OperationId = string.Join(":",
                span.OperationName,
                span.Tags.TryGetValue(CouchbaseTags.OperationId, out var id) ? id : "unknown"
            );
            TotalDuration = span.Duration;
            PopulateSummary(span.Spans);
        }

        private void PopulateSummary(IEnumerable<Span> spans)
        {
            foreach (var span in spans)
            {
                switch (span.OperationName)
                {
                    case CouchbaseOperationNames.RequestEncoding:
                        EncodingDuration += span.Duration;
                        break;
                    case CouchbaseOperationNames.DispatchToServer:
                        DispatchDuration += span.Duration;
                        LocalEndpoint = span.Tags.TryGetValue(CouchbaseTags.LocalAddress, out var local)
                            ? (string) local
                            : null;
                        RemoteEndpoint = span.Tags.TryGetValue(Tags.PeerAddress, out var remote)
                            ? (string) remote
                            : null;

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
