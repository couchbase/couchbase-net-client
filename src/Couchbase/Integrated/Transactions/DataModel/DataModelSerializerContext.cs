#nullable enable
using System.Text.Json.Serialization;

namespace Couchbase.Integrated.Transactions.DataModel
{
    // Only build metadata since we are only deserializing these types for now, the weight
    // of the serialization logic is unnecessary.
    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(QueryErrorCause))]
    internal partial class DataModelSerializerContext : JsonSerializerContext
    {
    }
}





