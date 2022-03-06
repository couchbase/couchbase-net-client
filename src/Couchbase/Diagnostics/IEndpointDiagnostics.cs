using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Diagnostics
{
    // We must put the System.Text.Json serialization directives on IEndpointDiagnostics rather than on EndpointDiagnostics
    // since the consumer will be building serializers based on the interface.

    public interface IEndpointDiagnostics
    {
        /// <summary>
        /// Gets the service type.
        /// </summary>
        [JsonIgnore]
        ServiceType Type { get; }

        /// <summary>
        /// Gets the report ID.
        /// </summary>
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Id { get; }

        /// <summary>
        /// Gets the local endpoint address including port.
        /// </summary>
        [JsonPropertyName("local")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Local { get; }

        /// <summary>
        /// Gets the remote endpoint address including port.
        /// </summary>
        [JsonPropertyName("remote")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Remote { get; }

        /// <summary>
        /// Gets the last activity for the service endpoint express as microseconds.
        /// </summary>
        [JsonPropertyName("last_activity_us")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        long? LastActivity { get; }

        /// <summary>
        /// Gets the latency for service endpoint expressed as microseconds.
        /// </summary>
        [JsonPropertyName("latency_us")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        long? Latency { get; }

        /// <summary>
        /// Gets the scope for the service endpoint.
        /// This could be the bucket name for <see cref="ServiceType.KeyValue"/> service endpoints.
        /// </summary>
        [JsonPropertyName("scope")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Scope { get; }

        /// <summary>
        /// Gets the service state.
        /// </summary>
        [JsonPropertyName("state")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ServiceState? State { get; }
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
