using System;
using Couchbase.Core.Sharding;
using System.Collections.Generic;
using Newtonsoft.Json;
using Couchbase.Utils;

namespace Couchbase.Core.Configuration.Server
{
    public class Ports : IEquatable<Ports>
    {
        public int direct { get; set; }

        public bool Equals(Ports other)
        {
            if (other == null) return false;
            return direct == other.direct;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Ports) obj);
        }

        public override int GetHashCode()
        {
            return direct;
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

    public class Node : IEquatable<Node>
    {
        public string couchApiBase { get; set; }
        public string hostname { get; set; }
        public Ports ports { get; set; }

        public bool Equals(Node other)
        {
            if (other == null) return false;
            return string.Equals(couchApiBase, other.couchApiBase) &&
                   string.Equals(hostname, other.hostname) &&
                   Equals(ports, other.ports);
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
                var hashCode = (couchApiBase != null ? couchApiBase.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (hostname != null ? hostname.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ports != null ? ports.GetHashCode() : 0);
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

    public class Services : IEquatable<Services>
    {
        public int mgmt { get; set; }
        public int mgmtSSL { get; set; }
        public int indexAdmin { get; set; }
        public int indexScan { get; set; }
        public int indexHttp { get; set; }
        public int indexStreamInit { get; set; }
        public int indexStreamCatchup { get; set; }
        public int indexStreamMaint { get; set; }
        public int indexHttps { get; set; }
        public int kv { get; set; }
        public int kvSSL { get; set; }
        public int capi { get; set; }
        public int capiSSL { get; set; }
        public int projector { get; set; }
        public int n1ql { get; set; }
        public int n1qlSSL { get; set; }
        public int cbas { get; set; }
        public int cbasSSL { get; set; }
        public int fts { get; set; }
        public int ftsSSL { get; set; }

        public bool Equals(Services other)
        {
            if (other == null) return false;
            return mgmt == other.mgmt && mgmtSSL == other.mgmtSSL && indexAdmin == other.indexAdmin &&
                   indexScan == other.indexScan && indexHttp == other.indexHttp &&
                   indexStreamInit == other.indexStreamInit && indexStreamCatchup == other.indexStreamCatchup &&
                   indexStreamMaint == other.indexStreamMaint && indexHttps == other.indexHttps && kv == other.kv &&
                   kvSSL == other.kvSSL && capi == other.capi && capiSSL == other.capiSSL &&
                   projector == other.projector && n1ql == other.n1ql && n1qlSSL == other.n1qlSSL;
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
                var hashCode = mgmt;
                hashCode = (hashCode * 397) ^ mgmtSSL;
                hashCode = (hashCode * 397) ^ indexAdmin;
                hashCode = (hashCode * 397) ^ indexScan;
                hashCode = (hashCode * 397) ^ indexHttp;
                hashCode = (hashCode * 397) ^ indexStreamInit;
                hashCode = (hashCode * 397) ^ indexStreamCatchup;
                hashCode = (hashCode * 397) ^ indexStreamMaint;
                hashCode = (hashCode * 397) ^ indexHttps;
                hashCode = (hashCode * 397) ^ kv;
                hashCode = (hashCode * 397) ^ kvSSL;
                hashCode = (hashCode * 397) ^ capi;
                hashCode = (hashCode * 397) ^ capiSSL;
                hashCode = (hashCode * 397) ^ projector;
                hashCode = (hashCode * 397) ^ n1ql;
                hashCode = (hashCode * 397) ^ n1qlSSL;
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

    public class NodesExt : IEquatable<NodesExt>
    {
        public Services services { get; set; }
        public bool thisNode { get; set; }
        public string hostname { get; set; }

        public bool Equals(NodesExt other)
        {
            if (other == null) return false;
            return Equals(services, other.services) &&
                   thisNode == other.thisNode &&
                   hostname == other.hostname;
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
                return ((services != null ? services.GetHashCode() : 0) * 397) ^ thisNode.GetHashCode();
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

    public class Ddocs
    {
        public string uri { get; set; }
    }

    //use Couchbase.Core.ShardingVBucket.ServerMap instead
   /* public class VBucketServerMap
    {
        public string hashAlgorithm { get; set; }
        public int numReplicas { get; set; }
        public List<string> serverList { get; set; }
        public List<List<int>> vBucketMap { get; set; }
    }*/

    //Root object
    public class BucketConfig : IEquatable<BucketConfig>
    {
        [JsonProperty("rev")]
        public uint Rev { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("streamingUri")]
        public string StreamingUri { get; set; }

        [JsonProperty("nodes")]
        public List<Node> Nodes { get; set; }

        [JsonProperty("nodesExt")]
        public List<NodesExt> NodesExt { get; set; }

        [JsonProperty("nodeLocator")]
        public string NodeLocator { get; set; }

        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("ddocs")]
        public Ddocs Ddocs { get; set; }

        [JsonProperty("vBucketServerMap")]
        public VBucketServerMap VBucketServerMap { get; set; }

        [JsonProperty("bucketCapabilitiesVer")]
        public string BucketCapabilitiesVer { get; set; }

        [JsonProperty("bucketCapabilities")]
        public List<string> BucketCapabilities { get; set; }

        public bool Equals(BucketConfig other)
        {
            if (other == null) return false;

            VBucketMapChanged = !Equals(VBucketServerMap, other.VBucketServerMap);
            ClusterNodesChanged = !(NodesExt.AreEqual(other.NodesExt) && Nodes.AreEqual(other.Nodes));

            return Rev == other.Rev && string.Equals(Name, other.Name) && string.Equals(Uri, other.Uri) &&
                   string.Equals(StreamingUri, other.StreamingUri) && string.Equals(NodeLocator, other.NodeLocator) &&
                   !ClusterNodesChanged && !VBucketMapChanged;
        }

        public bool VBucketMapChanged { get; private set; }

        public bool ClusterNodesChanged { get; private set; }

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
    }
}
