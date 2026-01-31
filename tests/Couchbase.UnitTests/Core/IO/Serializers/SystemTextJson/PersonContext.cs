using System;
using System.Text.Json.Serialization;
using Couchbase.Views;

namespace Couchbase.UnitTests.Core.IO.Serializers.SystemTextJson
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SystemTextJsonSerializerTests.Person))]
    [JsonSerializable(typeof(SystemTextJsonProjectionBuilderTests.ProjectionWrapper))]
#pragma warning disable CS0618 // Type or member is obsolete
    [JsonSerializable(typeof(ViewRow<string[], object>))]
#pragma warning restore CS0618 // Type or member is obsolete
    internal partial class PersonContext : JsonSerializerContext
    {
    }
}
