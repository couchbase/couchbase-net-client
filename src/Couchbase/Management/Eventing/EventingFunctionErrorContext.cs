using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core;
using Couchbase.Core.Retry;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Management.Eventing
{
    [InterfaceStability(Level.Uncommitted)]
    public class EventingFunctionErrorContext : IErrorContext
    {
        public string? Message { get; set; }

        // Ignore during serialization due to types not being available in ManagementSerializerContext.
        // If the property contained any type not registered and we didn't have this attribute,
        // ToString would throw an exception.
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public object? Info { get; set; }

        public List<RetryReason>? RetryReasons { get; internal set; }

        public override string ToString() =>
            JsonSerializer.Serialize(this, ManagementSerializerContext.Default.EventingFunctionErrorContext);
    }
}
