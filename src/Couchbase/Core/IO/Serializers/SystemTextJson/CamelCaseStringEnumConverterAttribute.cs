using System;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    internal class CamelCaseStringEnumConverter<T> : JsonStringEnumConverter<T> where T : struct, Enum
    {
        public CamelCaseStringEnumConverter() : base(JsonNamingPolicy.CamelCase)
        {
        }
    }
}
