using System;
using Couchbase.Core.Sharding;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using Couchbase.Utils;

namespace Couchbase.Core.Configuration.Server
{
    internal class Ports : IEquatable<Ports>
    {
        [JsonPropertyName("direct")] public int Direct { get; set; }
        [JsonPropertyName("proxy")] public int Proxy { get; set; }
        [JsonPropertyName("sslDirect")] public int SslDirect { get; set; }
        [JsonPropertyName("httpsCAPI")] public int HttpsCapi { get; set; }
        [JsonPropertyName("httpsMgmt")] public int HttpsMgmt { get; set; }

        public bool Equals(Ports other)
        {
            if (other == null) return false;
            return Direct == other.Direct &&
                Proxy == other.Proxy &&
                SslDirect == other.SslDirect &&
                HttpsCapi == other.HttpsCapi &&
                HttpsMgmt == other.HttpsMgmt;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Ports) obj);
        }

        public override int GetHashCode()
        {
            return Direct;
        }

        public static bool operator ==(Ports left, Ports right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Ports left, Ports right)
        {
            return !Equals(left, right);
        }
    }

    internal class Node : IEquatable<Node>
    {
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
        [JsonPropertyName("couchApiBase")] public string CouchApiBase { get; set; }
        [JsonPropertyName("couchApiBaseHttps")] public string CouchApiBaseHttps { get; set; }
        [JsonPropertyName("hostname")] public string Hostname { get; set; }
        [JsonPropertyName("ports")] public Ports Ports { get; set; }
        [JsonPropertyName("services")] public List<string> Services { get; set; }
        [JsonPropertyName("version")] public string Version { get; set; }

        public bool Equals(Node other)
        {
            if (other == null) return false;
            return string.Equals(CouchApiBase, other.CouchApiBase) &&
                   string.Equals(Hostname, other.Hostname) &&
                   Equals(Ports, other.Ports);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Node) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (CouchApiBase != null ? CouchApiBase.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Hostname != null ? Hostname.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Ports != null ? Ports.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(Node left, Node right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Node left, Node right)
        {
            return !Equals(left, right);
        }
    }

    internal class Services : IEquatable<Services>
    {
        [JsonPropertyName("mgmt")] public int Mgmt { get; set; }
        [JsonPropertyName("mgmtSSL")] public int MgmtSsl { get; set; }
        [JsonPropertyName("indexAdmin")] public int IndexAdmin { get; set; }
        [JsonPropertyName("indexScan")] public int IndexScan { get; set; }
        [JsonPropertyName("indexHttp")] public int IndexHttp { get; set; }
        [JsonPropertyName("indexStreamInit")] public int IndexStreamInit { get; set; }
        [JsonPropertyName("indexStreamCatchup")] public int IndexStreamCatchup { get; set; }
        [JsonPropertyName("indexStreamMaint")] public int IndexStreamMaint { get; set; }
        [JsonPropertyName("indexHttps")] public int IndexHttps { get; set; }
        [JsonPropertyName("kv")] public int Kv { get; set; }
        [JsonPropertyName("kvSSL")] public int KvSsl { get; set; }
        [JsonPropertyName("capi")] public int Capi { get; set; }
        [JsonPropertyName("capiSSL")] public int CapiSsl { get; set; }
        [JsonPropertyName("projector")] public int Projector { get; set; }
        [JsonPropertyName("n1ql")] public int N1Ql { get; set; }
        [JsonPropertyName("n1qlSSL")] public int N1QlSsl { get; set; }
        [JsonPropertyName("cbas")] public int Cbas { get; set; }
        [JsonPropertyName("cbasSSL")] public int CbasSsl { get; set; }
        [JsonPropertyName("fts")] public int Fts { get; set; }
        [JsonPropertyName("ftsSSL")] public int FtsSsl { get; set; }
        [JsonPropertyName("moxi")] public int Moxi { get; set; }
        [JsonPropertyName("eventingAdminPort")] public int EventingAdminPort { get; set; }
        [JsonPropertyName("eventingSSL")] public int EventingSSL { get; set; }

        public bool Equals(Services other)
        {
            if (other == null) return false;
            return Mgmt == other.Mgmt && MgmtSsl == other.MgmtSsl && IndexAdmin == other.IndexAdmin &&
                   IndexScan == other.IndexScan && IndexHttp == other.IndexHttp &&
                   IndexStreamInit == other.IndexStreamInit && IndexStreamCatchup == other.IndexStreamCatchup &&
                   IndexStreamMaint == other.IndexStreamMaint && IndexHttps == other.IndexHttps && Kv == other.Kv &&
                   KvSsl == other.KvSsl && Capi == other.Capi && CapiSsl == other.CapiSsl &&
                   Projector == other.Projector && N1Ql == other.N1Ql && N1QlSsl == other.N1QlSsl &&
                   EventingAdminPort == other.EventingAdminPort && EventingSSL == other.EventingSSL;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Services) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Mgmt;
                hashCode = (hashCode * 397) ^ MgmtSsl;
                hashCode = (hashCode * 397) ^ IndexAdmin;
                hashCode = (hashCode * 397) ^ IndexScan;
                hashCode = (hashCode * 397) ^ IndexHttp;
                hashCode = (hashCode * 397) ^ IndexStreamInit;
                hashCode = (hashCode * 397) ^ IndexStreamCatchup;
                hashCode = (hashCode * 397) ^ IndexStreamMaint;
                hashCode = (hashCode * 397) ^ IndexHttps;
                hashCode = (hashCode * 397) ^ Kv;
                hashCode = (hashCode * 397) ^ KvSsl;
                hashCode = (hashCode * 397) ^ Capi;
                hashCode = (hashCode * 397) ^ CapiSsl;
                hashCode = (hashCode * 397) ^ Projector;
                hashCode = (hashCode * 397) ^ N1Ql;
                hashCode = (hashCode * 397) ^ N1QlSsl;
                return hashCode;
            }
        }

        public static bool operator ==(Services left, Services right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Services left, Services right)
        {
            return !Equals(left, right);
        }
    }

    internal class NodesExt : IEquatable<NodesExt>
    {
        public NodesExt()
        {
            Services = new Services();
        }

        [JsonPropertyName("thisNode")] public bool ThisNode { get; set; }
        [JsonPropertyName("services")] public Services Services { get; set; }
        [JsonPropertyName("hostname")] public string Hostname { get; set; }
        [JsonPropertyName("alternateAddresses")] public Dictionary<string, ExternalAddressesConfig> AlternateAddresses { get; set; }

        public bool HasAlternateAddress => AlternateAddresses != null && AlternateAddresses.Any();

        public bool Equals(NodesExt other)
        {
            if (other == null) return false;
            return Equals(Services, other.Services) &&
                   Hostname == other.Hostname;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((NodesExt) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Services != null ? Services.GetHashCode() : 0) * 397);
            }
        }

        public static bool operator ==(NodesExt left, NodesExt right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(NodesExt left, NodesExt right)
        {
            return !Equals(left, right);
        }
    }

    internal class Ddocs
    {
        public string uri { get; set; }
    }

    //Root object
    internal class BucketConfig : IEquatable<BucketConfig>
    {
        private string _networkResolution = Couchbase.NetworkResolution.Auto;

        public BucketConfig()
        {
            Nodes = new List<Node>();
            VBucketServerMap = new VBucketServerMapDto();
        }

        public string NetworkResolution
        {
            get => _networkResolution;
            set
            {
                _networkResolution = value;

                //After setting the network resolution we need to update the
                //servers used for VBucketMapping so the correct addresses are used.
                ResolveHostName();
            }
        }

        /// <summary>
        /// Sets the "effective" network resolution to be used for alternate addresses using the following heuristic:
        /// "internal": The SDK should be using the normal addresses/ports as specified in the config.
        /// "external": The SDK should be using the "external" alternate-address address/ports as specified in the config.
        /// "auto" (default): The SDK should be making a determination based on the heuristic that is in the RFC at bootstrap
        /// time only, and then once this determination has been made, the network resolution mode should be unambiguously
        /// set to "internal" or "external".
        /// </summary>
        /// <param name="options">THe <see cref="ClusterOptions"/> for configuration.</param>
        public void SetEffectiveNetworkResolution(ClusterOptions options)
        {
            if (NodesExt.FirstOrDefault()!.HasAlternateAddress)
            {
                //Use heuristic to derive the network resolution from auto
                if (options.NetworkResolution == Couchbase.NetworkResolution.Auto)
                {
                    foreach (var nodesExt in NodesExt)
                    {
                        if (nodesExt.AlternateAddresses.Any())
                        {
                            NetworkResolution = options.EffectiveNetworkResolution =
                                Couchbase.NetworkResolution.External;
                            return;
                        }
                    }
                    //We detect internal or "default" should be used
                    NetworkResolution = options.EffectiveNetworkResolution = Couchbase.NetworkResolution.Default;
                    return;
                }
                else
                {
                    //use whatever the caller wants to use
                    NetworkResolution = options.EffectiveNetworkResolution = options.NetworkResolution;
                }
            }
            else
            {
                //we don't have any alt addresses so just use the internal address
                NetworkResolution = options.EffectiveNetworkResolution = Couchbase.NetworkResolution.Default;
            }
        }

        /// <summary>
        /// Set to true if a GCCCP config
        /// </summary>
        [JsonIgnore]
        public bool IsGlobal => Name == "CLUSTER";

        /// <summary>
        ///When true, we want to ignore the config revision and just accept the
        ///config provided. This happens when a DNS SRV refresh is detected and
        ///we need to "rebootstrap".
        /// </summary>
        [JsonIgnore] public bool IgnoreRev { get; set; }

        [JsonPropertyName("rev")] public ulong Rev { get; set; }
        [JsonPropertyName("revEpoch")] public ulong RevEpoch { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "CLUSTER";
        [JsonPropertyName("uri")] public string Uri { get; set; }
        [JsonPropertyName("streamingUri")] public string StreamingUri { get; set; }
        [JsonPropertyName("nodes")] public List<Node> Nodes { get; set; }
        [JsonPropertyName("nodesExt")] public List<NodesExt> NodesExt { get; set; }
        [JsonPropertyName("nodeLocator")] public string NodeLocator { get; set; }
        [JsonPropertyName("uuid")] public string Uuid { get; set; }
        [JsonPropertyName("ddocs")] public Ddocs Ddocs { get; set; }
        [JsonPropertyName("vBucketServerMap")] public VBucketServerMapDto VBucketServerMap { get; set; }
        [JsonPropertyName("bucketCapabilitiesVer")] public string BucketCapabilitiesVer { get; set; }
        [JsonPropertyName("bucketCapabilities")] public List<string> BucketCapabilities { get; set; }
        [JsonPropertyName("clusterCapabilitiesVer")] public List<int> ClusterCapabilitiesVersion { get; set; }
        [JsonPropertyName("clusterCapabilities")] public Dictionary<string, IEnumerable<string>> ClusterCapabilities { get; set; }

        public bool Equals(BucketConfig other)
        {
            if (other == null) return false;

            return Rev == other.Rev && RevEpoch == other.RevEpoch && string.Equals(Name, other.Name) && string.Equals(Uri, other.Uri) &&
                   string.Equals(StreamingUri, other.StreamingUri) && string.Equals(NodeLocator, other.NodeLocator) &&
                   Equals(VBucketServerMap, other.VBucketServerMap) && (NodesExt.AreEqual(other.NodesExt) && Nodes.AreEqual(other.Nodes));
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BucketConfig) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) Rev;
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Uri != null ? Uri.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (StreamingUri != null ? StreamingUri.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Nodes != null ? Nodes.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (NodesExt != null ? NodesExt.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (NodeLocator != null ? NodeLocator.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Uuid != null ? Uuid.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Ddocs != null ? Ddocs.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (VBucketServerMap != null ? VBucketServerMap.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (BucketCapabilitiesVer != null ? BucketCapabilitiesVer.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (BucketCapabilities != null ? BucketCapabilities.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(BucketConfig left, BucketConfig right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(BucketConfig left, BucketConfig right)
        {
            return !Equals(left, right);
        }

        public void ResolveHostName()
        {
            for (var i = 0; i < VBucketServerMap.ServerList.Length; i++)
            {
                var nodeExt = NodesExt?.FirstOrDefault(x => x.Hostname != null && VBucketServerMap.ServerList[i].Contains(x.Hostname));
                if (nodeExt != null && nodeExt.HasAlternateAddress && NetworkResolution == Couchbase.NetworkResolution.External)
                {
                    //The SSL port is resolved later
                    var alternateAddress = nodeExt.AlternateAddresses[NetworkResolution];
                    VBucketServerMap.ServerList[i] = alternateAddress.Hostname + ":" + alternateAddress.Ports.Kv;
                }
            }
        }
    }

    internal class ClusterCapabilities
    {
        [JsonPropertyName("clusterCapabilitiesVer")] public IEnumerable<int> Version { get; set; }
        [JsonPropertyName("clusterCapabilities")] public Dictionary<string, IEnumerable<string>> Capabilities { get; set; }

        internal bool EnhancedPreparedStatementsEnabled
        {
            get
            {
                if (Capabilities != null)
                {
                    if (Capabilities.TryGetValue(ServiceType.Query.GetDescription(), out var features))
                    {
                        return features.Contains(ClusterCapabilityFeatures.EnhancedPreparedStatements.GetDescription());
                    }
                }

                return false;
            }
        }

        internal bool UseReplicaEnabled
        {
            get
            {
                if (Capabilities != null)
                {
                    if (Capabilities.TryGetValue(ServiceType.Query.GetDescription(), out var features))
                    {
                        return features.Contains(ClusterCapabilityFeatures.UseReplicaFeature.GetDescription());
                    }
                }
                return false;
            }
        }
    }

    internal enum ClusterCapabilityFeatures
    {
        [Description("enhancedPreparedStatements")] EnhancedPreparedStatements,
        [Description("readFromReplica")] UseReplicaFeature
    }

    internal sealed class AlternateAddressesConfig
    {
        [JsonPropertyName("external")] public ExternalAddressesConfig External { get; set; }

        public bool HasExternalAddress => External?.Hostname != null;
    }

    internal sealed class ExternalAddressesConfig
    {
        [JsonPropertyName("hostname")] public string Hostname { get; set; }
        [JsonPropertyName("ports")] public Services Ports { get; set; }
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
