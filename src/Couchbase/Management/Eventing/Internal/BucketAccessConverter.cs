using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    /// <summary>
    /// This converter is used internally for mapping <see cref="EventingFunctionBucketAccess"/>
    /// </summary>
    internal class BucketAccessConverter : JsonConverter<EventingFunctionBucketAccess>
    {
        public override EventingFunctionBucketAccess Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && reader.ValueTextEquals("r"))
            {
                return EventingFunctionBucketAccess.ReadOnly;
            }

            return EventingFunctionBucketAccess.ReadWrite;
        }

        public override void Write(Utf8JsonWriter writer, EventingFunctionBucketAccess value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.GetDescription());
        }
    }
}
