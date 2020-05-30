using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Core.Diagnostics.Tracing
{
    interface ISpanContext
    {
        string SpanId { get; }
        string TraceId { get; }

        IEnumerable<KeyValuePair<string, string>> GetBaggageItems();
    }
}
