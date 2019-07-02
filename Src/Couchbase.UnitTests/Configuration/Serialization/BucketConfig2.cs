using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.UnitTests.Configuration.Serialization
{
    public class Ports
{
    public int proxy { get; set; }
    public int direct { get; set; }
    public int sslDirect { get; set; }
    public int httpsCAPI { get; set; }
    public int httpsMgmt { get; set; }
}

public class Node
{
    public string couchApiBase { get; set; }
    public string couchApiBaseHTTPS { get; set; }
    public double replication { get; set; }
    public object clusterMembership { get; set; }
    public object status { get; set; }
    public bool thisNode { get; set; }
    public string hostname { get; set; }
    public int clusterCompatibility { get; set; }
    public object version { get; set; }
    public object os { get; set; }
    public object otpNode { get; set; }
    public Ports ports { get; set; }
    public object services { get; set; }
}

public class Services
{
    public int fts { get; set; }
    public int ftsSSL { get; set; }
    public int mgmt { get; set; }
    public int moxi { get; set; }
    public int kv { get; set; }
    public int capi { get; set; }
    public int kvSSL { get; set; }
    public int capiSSL { get; set; }
    public int mgmtSSL { get; set; }
    public int projector { get; set; }
    public int indexAdmin { get; set; }
    public int indexScan { get; set; }
    public int indexHttp { get; set; }
    public int indexStreamInit { get; set; }
    public int indexStreamCatchup { get; set; }
    public int indexStreamMaint { get; set; }
    public int n1ql { get; set; }
    public int n1qlSSL { get; set; }
    public int cbas { get; set; }
    public int cbasSSL { get; set; }
}

public class External
{
    public string hostname { get; set; }
    public object ports { get; set; }
}

public class AlternateAddresses
{
    public External external { get; set; }
}

public class NodesExt
{
    public Services services { get; set; }
    public string hostname { get; set; }
    public AlternateAddresses alternateAddresses { get; set; }
}

public class Ddocs
{
    public string uri { get; set; }
}

public class VBucketServerMap
{
    public string hashAlgorithm { get; set; }
    public int numReplicas { get; set; }
    public List<string> serverList { get; set; }
    public List<List<int>> vBucketMap { get; set; }
    public List<object> vBucketMapForward { get; set; }
}

public class RootObject
{
    public string name { get; set; }
    public string bucketType { get; set; }
    public object authType { get; set; }
    public object saslPassword { get; set; }
    public int proxyPort { get; set; }
    public bool replicaIndex { get; set; }
    public string uri { get; set; }
    public string streamingUri { get; set; }
    public object terseBucketsBase { get; set; }
    public object terseStreamingBucketsBase { get; set; }
    public object localRandomKeyUri { get; set; }
    public object controllers { get; set; }
    public List<Node> nodes { get; set; }
    public List<NodesExt> nodesExt { get; set; }
    public object stats { get; set; }
    public Ddocs ddocs { get; set; }
    public string nodeLocator { get; set; }
    public string uuid { get; set; }
    public VBucketServerMap vBucketServerMap { get; set; }
    public int replicaNumber { get; set; }
    public int threadsNumber { get; set; }
    public object quota { get; set; }
    public object basicStats { get; set; }
    public string bucketCapabilitiesVer { get; set; }
    public List<string> bucketCapabilities { get; set; }
    public int rev { get; set; }
    public bool UseSsl { get; set; }
    public string SurrogateHost { get; set; }
    public object Username { get; set; }
    public string NetworkType { get; set; }
}
}
