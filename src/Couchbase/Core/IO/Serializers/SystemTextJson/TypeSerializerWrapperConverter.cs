using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    /// <summary>
    /// <see cref="JsonConverter"/> which serializes the content of <see cref="TypeSerializerWrapper"/>
    /// using its preferred <see cref="ITypeSerializer"/>.
    /// </summary>
    internal sealed class TypeSerializerWrapperConverter : JsonConverter<TypeSerializerWrapper>
    {
        public override bool HandleNull => true;

        public override TypeSerializerWrapper Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            ThrowHelper.ThrowNotSupportedException($"Cannot deserialize a {nameof(TypeSerializerWrapper)}.");
            return default;
        }

        public override void Write(Utf8JsonWriter writer, TypeSerializerWrapper value, JsonSerializerOptions options)
        {
            var innerValue = value.Value;

            // For performance reasons we take various simple types with well known formats in JSON and serialize them
            // directly as JSON literals rather than using the ITypeSerializer.
            switch (innerValue)
            {
                case null:
                    writer.WriteNullValue();
                    break;

                case bool boolValue:
                    writer.WriteBooleanValue(boolValue);
                    break;

                case string strValue:
                    writer.WriteStringValue(strValue);
                    break;

                case char charValue:
                    Span<char> charSpan = stackalloc char[1];
                    charSpan[0] = charValue;
                    writer.WriteStringValue(charSpan);
                    break;

                case uint uintValue:
                    writer.WriteNumberValue(uintValue);
                    break;

                case int intValue:
                    writer.WriteNumberValue(intValue);
                    break;

                case ulong ulongValue:
                    writer.WriteNumberValue(ulongValue);
                    break;

                case long longValue:
                    writer.WriteNumberValue(longValue);
                    break;

                case ushort ushortValue:
                    writer.WriteNumberValue(ushortValue);
                    break;

                case short shortValue:
                    writer.WriteNumberValue(shortValue);
                    break;

                case byte byteValue:
                    writer.WriteNumberValue(byteValue);
                    break;

                case sbyte sbyteValue:
                    writer.WriteNumberValue(sbyteValue);
                    break;

                case double doubleValue:
                    writer.WriteNumberValue(doubleValue);
                    break;

                case float floatValue:
                    writer.WriteNumberValue(floatValue);
                    break;

                case decimal decimalValue:
                    writer.WriteNumberValue(decimalValue);
                    break;

                default:
                {
                    // TODO: Replace with some approach with fewer heap allocations
                    using var stream = new MemoryStream();
                    value.Serializer.Serialize(stream, innerValue);

                    // Note: WriteRawValue is performing validation of JSON syntax by default, and will
                    // throw an exception if the stream contains invalid data.
                    writer.WriteRawValue(stream.TryGetBuffer(out var buffer)
                        ? buffer.AsSpan()
                        : stream.ToArray().AsSpan());

                    break;
                }
            }
        }
    }
}
