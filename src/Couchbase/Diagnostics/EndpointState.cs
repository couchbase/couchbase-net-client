using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Couchbase.Core.IO.Serializers.SystemTextJson;

namespace Couchbase.Diagnostics
{
    /// <summary>
    /// State of the connection to the endpoint.
    /// </summary>
    [JsonConverter(typeof(CamelCaseStringEnumConverter<EndpointState>))]
    public enum EndpointState
    {
        /// <summary>
        /// The endpoint socket is not reachable.
        /// </summary>
        [EnumMember(Value = "disconnected")]
        Disconnected,

        /// <summary>
        /// Currently connecting - including auth, etc.
        /// </summary>
        [EnumMember(Value = "connecting")]
        Connecting,

        /// <summary>
        /// Connected and ready.
        /// </summary>
        [EnumMember(Value = "connected")]
        Connected,

        /// <summary>
        /// Disconnected after being connected.
        /// </summary>
        [EnumMember(Value = "disconnecting")]
        Disconnecting
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
