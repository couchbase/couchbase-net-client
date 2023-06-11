using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    internal class SecondsTimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.Number)
            {
                ThrowHelper.ThrowJsonException("Number expected.");
            }

            return TimeSpan.FromSeconds(reader.GetInt64());
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue((long) value.TotalSeconds);
        }
    }
}
