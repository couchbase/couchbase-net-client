using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionConverter : JsonConverter<EventingFunction>
    {
        internal const string SuppressMessageJustification = "Consumer will be warned on original JsonSerializer call";

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = SuppressMessageJustification)]
        [UnconditionalSuppressMessage("Aot", "IL3050",
            Justification = SuppressMessageJustification)]
        public override EventingFunction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return (EventingFunction)JsonSerializer.Deserialize<EventingFunctionResponseDto>(ref reader, options);
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = SuppressMessageJustification)]
        [UnconditionalSuppressMessage("Aot", "IL3050",
            Justification = SuppressMessageJustification)]
        public override void Write(Utf8JsonWriter writer, EventingFunction value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, (EventingFunctionRequestDto) value, options);
        }
    }
}
