using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionBucketBindingConverter : JsonConverter<EventingFunctionBucketBinding>
    {
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = EventingFunctionConverter.SuppressMessageJustification)]
        public override EventingFunctionBucketBinding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return (EventingFunctionBucketBinding)JsonSerializer.Deserialize<EventingFunctionBucketBindingDto>(ref reader, options);
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = EventingFunctionConverter.SuppressMessageJustification)]
        public override void Write(Utf8JsonWriter writer, EventingFunctionBucketBinding value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, (EventingFunctionBucketBindingDto) value, options);
        }
    }
}
