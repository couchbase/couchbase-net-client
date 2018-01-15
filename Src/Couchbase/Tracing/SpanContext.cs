using System.Collections.Generic;
using System.Linq;
using OpenTracing;

namespace Couchbase.Tracing
{
    internal class SpanContext : ISpanContext
    {
        public long TraceId { get; }
        public long SpanId { get; }
        public long ParentId { get; }

        private IEnumerable<KeyValuePair<string, string>> Baggage { get; }

        public SpanContext(long traceId, long spanId, long parentId, IEnumerable<KeyValuePair<string, string>> baggage)
        {
            TraceId = traceId;
            SpanId = spanId;
            ParentId = parentId;
            Baggage = baggage;
        }

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            return Baggage ?? Enumerable.Empty<KeyValuePair<string, string>>();
        }
    }
}
