using System;
using System.Text.Json.Serialization;

namespace Couchbase.UnitTests.Core.IO.Serializers.SystemTextJson
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SystemTextJsonSerializerTests.Person))]
    [JsonSerializable(typeof(SystemTextJsonProjectionBuilderTests.ProjectionWrapper))]
    public partial class PersonContext : JsonSerializerContext
    {
    }
}
