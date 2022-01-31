using System;
using System.Text.Json.Serialization;
using Couchbase.Views;

namespace Couchbase.UnitTests.Core.IO.Serializers.SystemTextJson
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SystemTextJsonSerializerTests.Person))]
    [JsonSerializable(typeof(SystemTextJsonProjectionBuilderTests.ProjectionWrapper))]
    [JsonSerializable(typeof(ViewRow<string[], object>))]
    internal partial class PersonContext : JsonSerializerContext
    {
    }
}
