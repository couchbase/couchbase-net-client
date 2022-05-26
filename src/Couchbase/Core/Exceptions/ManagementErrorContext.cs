using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.Retry;
using Couchbase.Management;

#nullable enable

namespace Couchbase.Core.Exceptions
{
    public class ManagementErrorContext : IErrorContext
    {
        public string? Message { get; set; }
        public string? Statement { get; set; }
        public string? ClientContextId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HttpStatusCode HttpStatus { get; set; }

        public List<RetryReason>? RetryReasons { get; internal set; }

        public override string ToString() =>
            JsonSerializer.Serialize(this, ManagementSerializerContext.Default.ManagementErrorContext);
    }
}
