using System;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class StreamingHttp
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("bucketType")]
        public string BucketType { get; set; }

        [JsonProperty("authType")]
        public string AuthType { get; set; }

        [JsonProperty("saslPassword")]
        public string SaslPassword { get; set; }

        [JsonProperty("proxyPort")]
        public int ProxyPort { get; set; }

        [JsonProperty("replicaIndex")]
        public bool ReplicaIndex { get; set; }

        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("streamingUri")]
        public string StreamingUri { get; set; }

        [JsonProperty("localRandomKeyUri")]
        public string LocalRandomKeyUri { get; set; }

        [JsonProperty("controllers")]
        public Controllers Controllers { get; set; }

        [JsonProperty("nodes")]
        public Node[] Nodes { get; set; }

        [JsonProperty("stats")]
        public Stats Stats { get; set; }

        [JsonProperty("ddocs")]
        public Ddocs Ddocs { get; set; }

        [JsonProperty("nodeLocator")]
        public string NodeLocator { get; set; }

        [JsonProperty("autoCompactionSettings")]
        public bool AutoCompactionSettings { get; set; }

        [JsonProperty("fastWarmupSettings")]
        public bool FastWarmupSettings { get; set; }

        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("vBucketServerMap")]
        public VBucketServerMap VBucketServerMap { get; set; }

        [JsonProperty("bucketCapabilitiesVer")]
        public string BucketCapabilitiesVer { get; set; }

        [JsonProperty("bucketCapabilities")]
        public string[] BucketCapabilities { get; set; }
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