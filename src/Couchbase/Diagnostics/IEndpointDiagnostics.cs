namespace Couchbase.Diagnostics
{
    public interface IEndpointDiagnostics
    {
        /// <summary>
        /// Gets the service type.
        /// </summary>
        ServiceType Type { get; }

        /// <summary>
        /// Gets the report ID.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the local endpoint address including port.
        /// </summary>
        string Local { get; }

        /// <summary>
        /// Gets the remote endpoint address including port.
        /// </summary>
        string Remote { get; }

        /// <summary>
        /// Gets the last activity for the service endpoint express as microseconds.
        /// </summary>
        long? LastActivity { get; }

        /// <summary>
        /// Gets the latency for service endpoint expressed as microseconds.
        /// </summary>
        long? Latency { get; }

        /// <summary>
        /// Gets the scope for the service endpoint.
        /// This could be the bucket name for <see cref="ServiceType.KeyValue"/> service endpoints.
        /// </summary>
        string Scope { get; }

        /// <summary>
        /// Gets the service state.
        /// </summary>
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
