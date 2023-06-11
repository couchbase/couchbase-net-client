using System;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionDeploymentStatusConverter : JsonConverter<EventingFunctionDeploymentStatus>
    {
        public override EventingFunctionDeploymentStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.True => EventingFunctionDeploymentStatus.Deployed,
                JsonTokenType.False => EventingFunctionDeploymentStatus.Undeployed,
                _ => throw new JsonException()
            };

        public override void Write(Utf8JsonWriter writer, EventingFunctionDeploymentStatus value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value == EventingFunctionDeploymentStatus.Deployed);
        }
    }
}
