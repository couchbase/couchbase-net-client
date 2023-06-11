using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.Exceptions;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    internal class CompatibilityConverter : JsonConverter<EventingFunctionLanguageCompatibility>
    {
        public override EventingFunctionLanguageCompatibility
            Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            const string invalidMessage =
                "The EventingFunctionLanguageCompatibility value returned by the server is not supported by the client.";

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new InvalidArgumentException(invalidMessage);
            }

            return reader.GetString() switch
            {
                "6.0.0" => EventingFunctionLanguageCompatibility.Version_6_0_0,
                "6.5.0" => EventingFunctionLanguageCompatibility.Version_6_5_0,
                "6.6.2" => EventingFunctionLanguageCompatibility.Version_6_6_2,
                _ => throw new InvalidArgumentException(invalidMessage)
            };
        }

        public override void Write(Utf8JsonWriter writer, EventingFunctionLanguageCompatibility value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.GetDescription());
        }
    }
}
