using System;
using System.Text.Json;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionProcessingStatusConverter : System.Text.Json.Serialization.JsonConverter<EventingFunctionProcessingStatus>
    {
        public override EventingFunctionProcessingStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.True => EventingFunctionProcessingStatus.Running,
                JsonTokenType.False => EventingFunctionProcessingStatus.Paused,
                _ => throw new JsonException()
            };

        public override void Write(Utf8JsonWriter writer, EventingFunctionProcessingStatus value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value == EventingFunctionProcessingStatus.Running);
        }
    }
}
