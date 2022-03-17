using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.Utils;
using Couchbase.Utils;

namespace Couchbase.Query
{
    /// <summary>
    /// Serializes a <see cref="ScanVectorComponent"/> as JSON for a query.
    /// </summary>
    internal sealed class ScanVectorComponentJsonConverter : JsonConverter<ScanVectorComponent>
    {
        public override ScanVectorComponent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            ThrowHelper.ThrowNotSupportedException($"Cannot deserialize {nameof(ScanVectorComponent)}");
            return default;
        }

        public override void Write(Utf8JsonWriter writer, ScanVectorComponent value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.SequenceNumber);
            writer.WriteStringValue(value.VBucketUuid.ToStringInvariant());
            writer.WriteEndArray();
        }
    }
}
