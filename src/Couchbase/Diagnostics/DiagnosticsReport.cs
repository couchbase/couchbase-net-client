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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
