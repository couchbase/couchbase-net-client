using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Bootstrap
    {
        [JsonProperty("pools")]
        public Pool[] Pools { get; set; }

        [JsonProperty("isAdminCreds")]
        public bool IsAdminCreds { get; set; }

        [JsonProperty("settings")]
        public Settings Settings { get; set; }

        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("implementationVersion")]
        public string ImplementationVersion { get; set; }

        [JsonProperty("componentsVersion")]
        public ComponentsVersion ComponentsVersion { get; set; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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