using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization;
using Couchbase.Core.Retry;
using Couchbase.Management;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Core.Exceptions
{
    [InterfaceStability(Level.Uncommitted)]
    public class ManagementErrorContext : IErrorContext
    {
        public string? Message { get; set; }
        public string? Statement { get; set; }
        public string? ClientContextId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter<HttpStatusCode>))]
        public HttpStatusCode HttpStatus { get; set; }

        public List<RetryReason>? RetryReasons { get; internal set; }

        private static readonly JsonTypeInfo<ManagementErrorContext> RedactionSafeTypeInfo =
            RedactionSafeJson.TypeInfo<ManagementErrorContext>(ManagementSerializerContext.RedactionSafeOptions);

        public override string ToString() =>
            JsonSerializer.Serialize(this, RedactionSafeTypeInfo);
    }
}
