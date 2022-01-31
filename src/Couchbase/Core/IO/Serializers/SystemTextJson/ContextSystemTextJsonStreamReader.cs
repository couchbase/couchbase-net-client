using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    internal sealed class ContextSystemTextJsonStreamReader : SystemTextJsonStreamReader
    {
        private readonly JsonSerializerContext _context;

        public ContextSystemTextJsonStreamReader(Stream stream, JsonSerializerContext context)
            : base(stream, (context ?? throw new ArgumentNullException(nameof(context))).Options)
        {
            _context = context;
        }

        public override T? Deserialize<T>(JsonElement element) where T : default =>
            element.Deserialize<T>(GetTypeInfo<T>());

        protected override T? Deserialize<T>(ref Utf8JsonReader reader) where T : default =>
            JsonSerializer.Deserialize<T>(ref reader, GetTypeInfo<T>());

        private JsonTypeInfo<T> GetTypeInfo<T>()
        {
            // We don't want to require the consumer to include our internal types used by the
            // query system in their JsonSerializerContext. So we test for them and pull them
            // from our InternalSerializationContext. This also ensures they are deserialized
            // using our standard options.

            if (typeof(T) == typeof(Couchbase.Query.Error))
            {
                return (JsonTypeInfo<T>)(object) InternalSerializationContext.Default.QueryError;
            }
            else if (typeof(T) == typeof(Couchbase.Query.ErrorData))
            {
                return (JsonTypeInfo<T>)(object) InternalSerializationContext.Default.QueryErrorData;
            }
            else if (typeof(T) == typeof(Couchbase.Query.QueryWarning))
            {
                return (JsonTypeInfo<T>)(object) InternalSerializationContext.Default.QueryWarning;
            }
            else if (typeof(T) == typeof(Couchbase.Query.MetricsData))
            {
                return (JsonTypeInfo<T>)(object) InternalSerializationContext.Default.QueryMetricsData;
            }
            else if (typeof(T) == typeof(Couchbase.Analytics.WarningData))
            {
                return (JsonTypeInfo<T>)(object) InternalSerializationContext.Default.AnalyticsWarningData;
            }
            else if (typeof(T) == typeof(Couchbase.Analytics.MetricsData))
            {
                return (JsonTypeInfo<T>)(object) InternalSerializationContext.Default.AnalyticsMetricsData;
            }

            return _context.GetTypeInfo<T>();
        }
    }
}
