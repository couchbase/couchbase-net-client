using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    /// <summary>
    /// Serializes a nullable enumeration using strings from <see cref="DescriptionAttribute"/> annotations.
    /// </summary>
    /// <typeparam name="T">Type of the enumeration.</typeparam>
    internal sealed class NullableEnumDescriptionJsonConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T> : JsonConverter<T?>
        where T : struct, Enum
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (str is null)
            {
                return null;
            }

            if (!EnumExtensions.TryGetFromDescription<T>(str, out var value))
            {
                throw new JsonException();
            }

            return value;
        }

        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value.GetValueOrDefault().GetDescription());
            }
        }
    }
}
