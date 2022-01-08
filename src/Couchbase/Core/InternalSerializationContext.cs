using System;
using System.Text.Json.Serialization;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;

namespace Couchbase.Core
{
    /// <summary>
    /// <see cref="JsonSerializerContext"/> capable of serializing and deserializing various internal types
    /// used by the Couchbase SDK to communicate with Couchbase Server.
    /// </summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
    [JsonSerializable(typeof(BucketConfig))]
    [JsonSerializable(typeof(ErrorMapDto))]
    [JsonSerializable(typeof(Hello.HelloKey))]
    [JsonSerializable(typeof(Manifest))]
    internal partial class InternalSerializationContext : JsonSerializerContext
    {
    }
}
