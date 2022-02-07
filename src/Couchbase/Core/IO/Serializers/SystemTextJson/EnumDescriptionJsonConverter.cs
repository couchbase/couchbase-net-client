using System;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    /// <summary>
    /// Serializes an enumeration using strings from <see cref="DescriptionAttribute"/> annotations.
    /// </summary>
    /// <typeparam name="T">Type of the enumeration.</typeparam>
    internal sealed class EnumDescriptionJsonConverter<T> : JsonConverter<T>
        where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (str is null || !EnumExtensions.TryGetFromDescription<T>(str, out var value))
            {
                throw new JsonException();
            }

            return value;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.GetDescription());
        }
    }
}
