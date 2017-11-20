using System;
using System.Collections.Generic;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Core.Monitoring
{
    internal class DiagnosticsReport : IDiagnosticsReport
    {
        internal DiagnosticsReport(string reportId, Dictionary<string, IEnumerable<IEndpointDiagnostics>> services)
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
        public Dictionary<string, IEnumerable<IEndpointDiagnostics>> Services { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }
    }
}
