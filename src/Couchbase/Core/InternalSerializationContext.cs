using System;
using System.Text.Json.Serialization;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.IO.Serializers.SystemTextJson;

namespace Couchbase.Core
{
    /// <summary>
    /// <see cref="JsonSerializerContext"/> capable of serializing and deserializing various internal types
    /// used by the Couchbase SDK to communicate with Couchbase Server.
    /// </summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
    [JsonSerializable(typeof(BucketConfig))]
    [JsonSerializable(typeof(ErrorMapDto))]
    [JsonSerializable(typeof(Hello.HelloKey))]
    [JsonSerializable(typeof(Manifest))]
    [JsonSerializable(typeof(Query.QueryOptionsDto))]
    [JsonSerializable(typeof(Query.ScanVectorComponent))]
    [JsonSerializable(typeof(TypeSerializerWrapper))]
    [JsonSerializable(typeof(Query.Error), TypeInfoPropertyName = "QueryError")]
    [JsonSerializable(typeof(Query.ErrorData), TypeInfoPropertyName = "QueryErrorData")]
    [JsonSerializable(typeof(Query.QueryWarning), TypeInfoPropertyName = "QueryWarning")]
    [JsonSerializable(typeof(Query.MetricsData), TypeInfoPropertyName = "QueryMetricsData")]
    [JsonSerializable(typeof(Analytics.WarningData), TypeInfoPropertyName = "AnalyticsWarningData")]
    [JsonSerializable(typeof(Analytics.MetricsData), TypeInfoPropertyName = "AnalyticsMetricsData")]
    internal partial class InternalSerializationContext : JsonSerializerContext
    {
    }
}
