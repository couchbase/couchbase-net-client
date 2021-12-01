#if FALSE

// TODO: Use context for tests once CI agents have the .NET 6 SDK and support source generation

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

#endif
