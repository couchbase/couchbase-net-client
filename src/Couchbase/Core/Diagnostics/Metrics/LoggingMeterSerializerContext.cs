using System.Text.Json.Serialization;

namespace Couchbase.Core.Diagnostics.Metrics
{
    [JsonSerializable(typeof(LoggingMeterReport))]
    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    internal sealed partial class LoggingMeterSerializerContext : JsonSerializerContext;
}
