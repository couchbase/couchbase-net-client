using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Couchbase.Core.IO.Serializers.SystemTextJson;

#nullable enable

namespace Couchbase.Query
{
    /// <summary>
    /// Serialization POCO used by <see cref="QueryOptions"/> to create a JSON body for a query request.
    /// </summary>
    internal class QueryOptionsDto
    {
        #region Query

        [JsonPropertyName("prepared")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Prepared { get; set; }

        [JsonPropertyName("encoded_plan")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PreparedEncoded { get; set; }

        [JsonPropertyName("statement")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Statement { get; set; }

        #endregion

        #region Parameters

        [JsonPropertyName("args")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<TypeSerializerWrapper>? Arguments { get; set; }

        // Note: Should be filled with TypeSerializerWrapper objects only, but must have object values
        // to be compatible with System.Text.Json JsonExtensionData.
        [JsonExtensionData]
        public Dictionary<string, object>? AdditionalProperties { get; set; }

        #endregion

        #region Options

        [JsonPropertyName("auto_execute")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool AutoExecute { get; set; }

        [JsonPropertyName("client_context_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ClientContextId { get; set; }

        [JsonPropertyName("use_fts")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool FlexIndex { get; set; }

        [JsonPropertyName("metrics")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IncludeMetrics { get; set; }

        [JsonPropertyName("max_parallelism")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MaxServerParallelism { get; set; }

        [JsonPropertyName("preserve_expiry")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool PreserveExpiry { get; set; }

        [JsonPropertyName("pipeline_batch")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? PipelineBatch { get; set; }

        [JsonPropertyName("pipeline_cap")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? PipelineCapacity { get; set; }

        [JsonPropertyName("profile")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonConverter(typeof(EnumDescriptionJsonConverter<QueryProfile>))]
        public QueryProfile Profile { get; set; }

        [JsonPropertyName("query_context")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? QueryContext { get; set; }

        [JsonPropertyName("readonly")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? ReadOnly { get; set; }

        [JsonPropertyName("scan_cap")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ScanCapacity { get; set; }

        [JsonPropertyName("scan_consistency")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(NullableEnumDescriptionJsonConverter<QueryScanConsistencyInternal>))]
        public QueryScanConsistencyInternal? ScanConsistency { get; set; }

        [JsonPropertyName("scan_vectors")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, Dictionary<string, ScanVectorComponent>>? ScanVectors { get; set; }

        [JsonPropertyName("scan_wait")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(MillisecondsStringJsonConverter))]
        public TimeSpan? ScanWait { get; set; }

        [JsonPropertyName("timeout")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(MillisecondsStringJsonConverter))]
        public TimeSpan? Timeout { get; set; }

        #endregion

        #region ToDictionary

        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(QueryOptionsDto))]
        public Dictionary<string, object?> ToDictionary()
        {
            // This method isn't very efficient, but it's primarily here for backward compatibility and isn't normally used

            var result = new Dictionary<string, object?>();
            foreach (var property in typeof(QueryOptionsDto).GetProperties()
                         .Where(p => p.CanRead && p.GetCustomAttribute<JsonExtensionDataAttribute>() is null && p.Name != nameof(QueryOptionsDto.Arguments)))
            {
                var value = property.GetValue(this);

                var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition ?? JsonIgnoreCondition.Never;
                if (ignore == JsonIgnoreCondition.Always)
                {
                    continue;
                }

                if (ignore == JsonIgnoreCondition.WhenWritingNull && value is null)
                {
                    // Note: Nullable<T> is boxed as null, so this is checking that case as well
                    continue;
                }

                if (ignore == JsonIgnoreCondition.WhenWritingDefault && value is ValueType)
                {
                    var defaultValue = Activator.CreateInstance(value.GetType());
                    if (value.Equals(defaultValue))
                    {
                        continue;
                    }
                }

                var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
                result.Add(name, value);
            }

            if (Arguments is not null)
            {
                result.Add("args", Arguments.Select(p => p.Value).ToList());
            }

            if (AdditionalProperties is not null)
            {
                foreach (var additionalProperty in AdditionalProperties)
                {
                    result.Add(additionalProperty.Key, (additionalProperty.Value as TypeSerializerWrapper?)?.Value ?? additionalProperty.Value);
                }
            }

            return result;
        }

        #endregion
    }
}
