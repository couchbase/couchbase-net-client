using System;
using System.Text.Json.Serialization;

namespace Couchbase.UnitTests.Core.IO.Serializers.SystemTextJson
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SystemTextJsonSerializerTests.Person))]
    public partial class PersonContext : JsonSerializerContext
    {
    }
}
