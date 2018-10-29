using System;
using System.Collections.Generic;
using Couchbase.Core;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    public interface IBucketConfig : IEquatable<BucketConfig>
    {
        [JsonProperty("name")]
        string Name { get; set; }

        [JsonProperty("bucketType")]
        string BucketType { get; set; }

        [JsonProperty("authType")]
        string AuthType { get; set; }

        [JsonProperty("saslPassword")]
        string SaslPassword { get; set; }

        [JsonProperty("proxyPort")]
        int ProxyPort { get; set; }

        [JsonProperty("replicaIndex")]
        bool ReplicaIndex { get; set; }

        [JsonProperty("uri")]
        string Uri { get; set; }

        [JsonProperty("streamingUri")]
        string StreamingUri { get; set; }

        [JsonProperty("localRandomKeyUri")]
        string LocalRandomKeyUri { get; set; }

        [JsonProperty("controllers")]
        Controllers Controllers { get; set; }

        [JsonProperty("nodes")]
        Node[] Nodes { get; set; }

        [JsonProperty("nodesExt")]
        NodeExt[] NodesExt { get; set; }

        [JsonProperty("stats")]
        Stats Stats { get; set; }

        [JsonProperty("ddocs")]
        Ddocs Ddocs { get; set; }

        [JsonProperty("nodeLocator")]
        string NodeLocator { get; set; }

        [JsonProperty("uuid")]
        string Uuid { get; set; }

        [JsonProperty("vBucketServerMap")]
        VBucketServerMap VBucketServerMap { get; set; }

        [JsonProperty("replicaNumber")]
        int ReplicaNumber { get; set; }

        [JsonProperty("threadsNumber")]
        int ThreadsNumber { get; set; }

        [JsonProperty("quota")]
        Quota Quota { get; set; }

        [JsonProperty("basicStats")]
        BasicStats BasicStats { get; set; }

        [JsonProperty("bucketCapabilitiesVer")]
        string BucketCapabilitiesVer { get; set; }

        [JsonProperty("bucketCapabilities")]
        string[] BucketCapabilities { get; set; }

        [JsonProperty("terseBucketsBase")]
        string TerseUri { get; set; }

        [JsonProperty("terseStreamingBucketsBase")]
        string TerseStreamingUri { get; set; }

        [JsonProperty("rev")]
        uint Rev { get; set; }

        string SurrogateHost { get; set; }

        string Password { get; set; }

        string Username { get; set; }

        bool UseSsl { get; set; }

        Node GetRandomNode();

        Uri GetTerseStreamingUri(Node node, bool useSsl);

        Uri GetTerseUri(Node node, bool useSsl);

        Uri GetStreamingUri(Node node, bool useSsl);

        Uri GetUri(Node node, bool useSsl);

        bool AreNodesEqual(IBucketConfig other);

        bool IsVBucketServerMapEqual(IBucketConfig other);

        string NetworkType {get; set; }
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
