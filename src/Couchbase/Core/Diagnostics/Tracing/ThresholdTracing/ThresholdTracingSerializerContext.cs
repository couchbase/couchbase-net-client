using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Core.Diagnostics.Tracing.ThresholdTracing
{
    [JsonSerializable(typeof(IDictionary<string, ThresholdSummaryReport>))]
    internal partial class ThresholdTracingSerializerContext : JsonSerializerContext
    {
    }
}
