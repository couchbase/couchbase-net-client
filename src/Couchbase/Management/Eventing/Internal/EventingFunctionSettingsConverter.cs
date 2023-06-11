using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionSettingsConverter : JsonConverter<EventingFunctionSettings>
    {
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = EventingFunctionConverter.SuppressMessageJustification)]
        public override EventingFunctionSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return (EventingFunctionSettings)JsonSerializer.Deserialize<EventingFunctionSettingsResponseDto>(ref reader, options);
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = EventingFunctionConverter.SuppressMessageJustification)]
        public override void Write(Utf8JsonWriter writer, EventingFunctionSettings value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, (EventingFunctionSettingsRequestDto) value, options);
        }
    }
}
