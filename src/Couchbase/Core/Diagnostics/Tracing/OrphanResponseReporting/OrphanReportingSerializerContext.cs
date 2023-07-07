using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting
{
    // Include fields because OrphanReport uses fields so they can be passed by reference
    [JsonSourceGenerationOptions(IncludeFields = true)]
    [JsonSerializable(typeof(OrphanReport))]
    internal partial class OrphanReportingSerializerContext : JsonSerializerContext
    {
    }
}
