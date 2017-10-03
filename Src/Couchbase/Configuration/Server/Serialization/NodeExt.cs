using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    /// <summary>
    /// Represents the nodesExt element of a server configuration; the
    /// extended set of services that a node is configured to have (data, query, index, etc)
    /// </summary>
    public sealed class NodeExt
    {
        public NodeExt()
        {
            Services = new Services();
        }

        /// <summary>
        /// Gets or sets the services that this node has available.
        /// </summary>
        /// <value>
        /// The services.
        /// </value>
        [JsonProperty("services")]
        public Services Services { get; set; }

        /// <summary>
        /// Gets or sets the hostname or IP address of this node.
        /// </summary>
        /// <value>
        /// The hostname.
        /// </value>
        [JsonProperty("hostname")]
        public string Hostname { get; set; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
