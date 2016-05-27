using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    public sealed class Node : IEquatable<Node>
    {
        const string LocalHost = "127.0.0.1";
        private const string HostToken = "$HOST";
        private const string ViewPort = "8091";

        public Node()
        {
            Ports = new Ports
            {
                Direct = 11210,
                Proxy = 11211,
                SslDirect = 11207,
                HttpsCapi = 18092,
                HttpsMgmt = 18091
            };
            CouchApiBase = "http://$HOST:8092/default";
            CouchApiBaseHttps = "https://$HOST:18092/default";
        }

        [JsonProperty("couchApiBase")]
        public string CouchApiBase { get; set; }

        [JsonProperty("couchApiBaseHTTPS")]
        public string CouchApiBaseHttps { get; set; }

        [JsonProperty("replication")]
        public double Replication { get; set; }

        [JsonProperty("clusterMembership")]
        public string ClusterMembership { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("thisNode")]
        public bool ThisNode { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }

        [JsonProperty("clusterCompatibility")]
        public int ClusterCompatibility { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("os")]
        public string Os { get; set; }

        [JsonProperty("otpNode")]
        public string OtpNode { get; set; }

        [JsonProperty("ports")]
        public Ports Ports { get; set; }

        [JsonProperty("services")]
        public List<string> Services { get; set; }

        public IPEndPoint GetMemcachedEndPoint(bool useInterNetworkV6Addresses)
        {
            var hostName = Hostname.Replace(HostToken, LocalHost);
            hostName = hostName.Replace(ViewPort, Ports.Direct.ToString(CultureInfo.InvariantCulture));
            return IPEndPointExtensions.GetEndPoint(hostName);
        }

        public bool Equals(Node other)
        {
            return (other != null &&
                    other.Hostname == Hostname);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Node);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Hostname.GetHashCode();
                return hash;
            }
        }
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