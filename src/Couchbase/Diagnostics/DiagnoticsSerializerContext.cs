using System;
using System.Text.Json.Serialization;

namespace Couchbase.Diagnostics
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(IDiagnosticsReport))]
    [JsonSerializable(typeof(IPingReport))]
    [JsonSerializable(typeof(IEndpointDiagnostics))]
    internal partial class DiagnoticsSerializerContext : JsonSerializerContext
    {
    }
}
