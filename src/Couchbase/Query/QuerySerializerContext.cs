using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.IO.Serializers.SystemTextJson;

#nullable enable

namespace Couchbase.Query
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
    [JsonSerializable(typeof(QueryOptionsDto))]
    [JsonSerializable(typeof(ScanVectorComponent))]
    [JsonSerializable(typeof(TypeSerializerWrapper))]
    [JsonSerializable(typeof(Error), TypeInfoPropertyName = "QueryError")]
    [JsonSerializable(typeof(ErrorData), TypeInfoPropertyName = "QueryErrorData")]
    [JsonSerializable(typeof(QueryWarning), TypeInfoPropertyName = "QueryWarning")]
    [JsonSerializable(typeof(MetricsData), TypeInfoPropertyName = "QueryMetricsData")]
    [JsonSerializable(typeof(QueryPlan))]
    [JsonSerializable(typeof(Core.Exceptions.Query.QueryErrorContext))]
    internal partial class QuerySerializerContext : JsonSerializerContext
    {
    }
}
