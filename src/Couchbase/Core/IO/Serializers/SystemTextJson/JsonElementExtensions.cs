using System;
using System.Diagnostics;
using System.Text.Json;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    /// <summary>
    /// Extensions for <see cref="JsonElement"/>.
    /// </summary>
    internal static class JsonElementExtensions
    {
        public static bool TryGetValue<T>(this JsonElement element, out T? value)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
                    {
                        if (element.TryGetInt32(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(long) || typeof(T) == typeof(long?))
                    {
                        if (element.TryGetInt64(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(double) || typeof(T) == typeof(double?))
                    {
                        if (element.TryGetDouble(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(short) || typeof(T) == typeof(short?))
                    {
                        if (element.TryGetInt16(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?))
                    {
                        if (element.TryGetDecimal(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(byte) || typeof(T) == typeof(byte?))
                    {
                        if (element.TryGetByte(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(float) || typeof(T) == typeof(float?))
                    {
                        if (element.TryGetSingle(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(uint) || typeof(T) == typeof(uint?))
                    {
                        if (element.TryGetUInt32(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(ushort) || typeof(T) == typeof(ushort?))
                    {
                        if (element.TryGetUInt16(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(ulong) || typeof(T) == typeof(ulong?))
                    {
                        if (element.TryGetUInt64(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(sbyte) || typeof(T) == typeof(sbyte?))
                    {
                        if (element.TryGetSByte(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }
                    break;

                case JsonValueKind.String:
                    if (typeof(T) == typeof(string))
                    {
                        value = (T)(object) element.GetString()!;
                        return true;
                    }

                    if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
                    {
                        if (element.TryGetDateTime(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(DateTimeOffset?))
                    {
                        if (element.TryGetDateTimeOffset(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(Guid) || typeof(T) == typeof(Guid?))
                    {
                        if (element.TryGetGuid(out var tempValue))
                        {
                            value = (T)(object) tempValue;
                            return true;
                        }
                    }

                    if (typeof(T) == typeof(char) || typeof(T) == typeof(char?))
                    {
                        string str = element.GetString()!;
                        if (str.Length == 1)
                        {
                            value = (T)(object)str[0];
                            return true;
                        }
                    }
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                    {
                        value = (T)(object)element.GetBoolean();
                        return true;
                    }
                    break;

                case JsonValueKind.Null:
                    value = default!;
                    return !typeof(T).IsValueType ||
                           (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>));
            }

            value = default!;
            return false;
        }
    }
}
