using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.Exceptions;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionStatusConverter : JsonConverter<EventingFunctionStatus>
    {
        public override EventingFunctionStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            const string invalidMessage =
                "The EventingFunctionStatus value returned by the server is not supported by the client.";

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new InvalidArgumentException(invalidMessage);
            }

            return reader.GetString() switch
            {
                "deployed" => EventingFunctionStatus.Deployed,
                "deploying" => EventingFunctionStatus.Deploying,
                "paused" => EventingFunctionStatus.Paused,
                "pausing" => EventingFunctionStatus.Pausing,
                "undeploying" => EventingFunctionStatus.UnDeploying,
                "undeployed" => EventingFunctionStatus.Undeployed,
                _ => throw new InvalidArgumentException(invalidMessage)
            };
        }

        public override void Write(Utf8JsonWriter writer, EventingFunctionStatus value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString().ToLower());
        }
    }
}
