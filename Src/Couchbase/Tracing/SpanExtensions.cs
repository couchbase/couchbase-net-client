using OpenTracing;

namespace Couchbase.Tracing
{
    public static class SpanExtensions
    {
        public static void SetPeerLatencyTag(this ISpan span, long? latency)
        {
            if (latency.HasValue)
            {
                span.SetTag(CouchbaseTags.PeerLatency, $"{latency.Value}us");
            }
        }

        public static void SetPeerLatencyTag(this ISpan span, string latency)
        {
            if (!string.IsNullOrWhiteSpace(latency))
            {
                span.SetTag(CouchbaseTags.PeerLatency, latency);
            }
        }
    }
}
