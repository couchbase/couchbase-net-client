using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

#nullable enable

namespace Couchbase.Utils
{
    internal static class HttpContentExtensions
    {
        public static bool TryDeserialize<T>(this string jsonString, JsonTypeInfo<T> typeInfo,
            [NotNullWhen(true)] out T? result)
        {
            try
            {
                result = JsonSerializer.Deserialize(jsonString, typeInfo);
                return result is not null;
            }
            catch (JsonException)
            {
                result = default;
                return false;
            }
        }
    }
}
