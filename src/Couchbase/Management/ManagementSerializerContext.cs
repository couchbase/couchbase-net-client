using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Couchbase.Core.Exceptions;
using Couchbase.Management.Buckets;
using Couchbase.Management.Eventing;

namespace Couchbase.Management
{
    /// <summary>
    /// Internal <see cref="JsonSerializerContext"/> used for management operations.
    /// </summary>
    /// <remarks>
    /// This is separate from the context used for general internal operations as an optimization.
    /// There is some small cost to the static constructor of a JsonSerializerContext, which scales based
    /// on the number of types included. Since management operations are more rarely used than others
    /// we keep them on a separate context.
    /// </remarks>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(BucketSettings))]
    [JsonSerializable(typeof(List<BucketSettings>))]
    [JsonSerializable(typeof(ManagementErrorContext))]
    [JsonSerializable(typeof(EventingFunctionErrorContext))]
    internal partial class ManagementSerializerContext : JsonSerializerContext
    {
    }
}
