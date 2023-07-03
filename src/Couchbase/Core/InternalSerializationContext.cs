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
    [JsonSerializable(typeof(TypeSerializerWrapper))]
    [JsonSerializable(typeof(Analytics.WarningData), TypeInfoPropertyName = "AnalyticsWarningData")]
    [JsonSerializable(typeof(Analytics.MetricsData), TypeInfoPropertyName = "AnalyticsMetricsData")]
    [JsonSerializable(typeof(Version.ClusterVersionProvider.Pools))]
    [JsonSerializable(typeof(Exceptions.KeyValue.KeyValueErrorContext))]
    [JsonSerializable(typeof(Exceptions.Analytics.AnalyticsErrorContext))]
    [JsonSerializable(typeof(Exceptions.Search.SearchErrorContext))]
    [JsonSerializable(typeof(Exceptions.View.ViewContextError))]
    internal partial class InternalSerializationContext : JsonSerializerContext
    {
#nullable enable
        public static string SerializeWithFallback<TValue>(TValue value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<TValue> jsonTypeInfo)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Serialize<TValue>(value, jsonTypeInfo);
            }
            catch (NotSupportedException)
            {
                try
                {
                    using var memoryStream = new System.IO.MemoryStream();
                    Couchbase.Core.IO.Serializers.DefaultSerializer.Instance.Serialize(memoryStream, value);
                    return System.Text.Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
                }
                catch (Exception)
                {
                    // do nothing.  Re-throw the original exception.
                }

                throw;
            }
        }

        public static void SerializeWithFallback<TValue>(System.IO.Stream stream, TValue value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<TValue> jsonTypeInfo)
        {
            try
            {
                System.Text.Json.JsonSerializer.Serialize<TValue>(stream, value, jsonTypeInfo);
            }
            catch (NotSupportedException)
            {
                try
                {
                    Couchbase.Core.IO.Serializers.DefaultSerializer.Instance.Serialize(stream, value);
                    return;
                }
                catch (Exception)
                {
                    // do nothing.  Re-throw the original exception.
                }

                throw;
            }
        }
    }
}
