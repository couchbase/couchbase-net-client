using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.Exceptions;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionDcpBoundaryConverter : JsonConverter<EventingFunctionDcpBoundary>
    {
        public override EventingFunctionDcpBoundary Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            const string invalidMessage =
                "The EventingFunctionDcpBoundary value returned by the server is not supported by the client.";

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new InvalidArgumentException(invalidMessage);
            }

            return reader.GetString() switch
            {
                "everything" => EventingFunctionDcpBoundary.Everything,
                "from_now" => EventingFunctionDcpBoundary.FromNow,
                _ => throw new InvalidArgumentException(invalidMessage)
            };
        }

        public override void Write(Utf8JsonWriter writer, EventingFunctionDcpBoundary value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.GetDescription());
        }
    }
}
