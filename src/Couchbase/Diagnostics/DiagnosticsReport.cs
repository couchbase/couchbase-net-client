using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Diagnostics
{
    internal class DiagnosticsReport : IDiagnosticsReport
    {
        internal DiagnosticsReport(string reportId, ConcurrentDictionary<string, IEnumerable<IEndpointDiagnostics>> services)
        {
            Id = reportId;
            Services = services;
        }

        [JsonIgnore]
        public DateTime CreatedAt { get; } = DateTime.UtcNow;

        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("version")]
        public short Version { get; } = 1;

        [JsonProperty("sdk")]
        public string Sdk { get; } = ClientIdentifier.GetClientDescription();

        [JsonProperty("services")]
        public IDictionary<string, IEnumerable<IEndpointDiagnostics>> Services { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }
    }
}
