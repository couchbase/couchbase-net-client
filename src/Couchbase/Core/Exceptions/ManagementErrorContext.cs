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

        //Bound from RedactionSafeOptions rather than the context's own JsonTypeInfo so that
        //redaction tags serialize as literal markers. Cached because resolving type info is a
        //dictionary lookup, and this runs on every ToString().
        private static readonly JsonTypeInfo<ManagementErrorContext> RedactionSafeTypeInfo =
            (JsonTypeInfo<ManagementErrorContext>)ManagementSerializerContext.RedactionSafeOptions.GetTypeInfo(typeof(ManagementErrorContext));

        public override string ToString() =>
            JsonSerializer.Serialize(this, RedactionSafeTypeInfo);
    }
}
