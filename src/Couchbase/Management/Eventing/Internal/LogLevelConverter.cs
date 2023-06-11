using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.Exceptions;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    internal class LogLevelConverter : JsonConverter<EventingFunctionLogLevel>
    {
        public override EventingFunctionLogLevel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            const string invalidMessage =
                "The EventingFunctionLogLevel value returned by the server is not supported by the client.";

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new InvalidArgumentException(invalidMessage);
            }

            return reader.GetString() switch
            {
                "INFO" => EventingFunctionLogLevel.Info,
                "ERROR" => EventingFunctionLogLevel.Error,
                "WARNING" => EventingFunctionLogLevel.Warning,
                "DEBUG" => EventingFunctionLogLevel.Debug,
                "TRACE" => EventingFunctionLogLevel.Trace,
                _ => throw new InvalidArgumentException(invalidMessage)
            };
        }

        public override void Write(Utf8JsonWriter writer, EventingFunctionLogLevel value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.GetDescription());
        }
    }
}
