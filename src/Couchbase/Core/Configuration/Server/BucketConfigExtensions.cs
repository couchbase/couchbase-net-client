using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Sharding;

namespace Couchbase.Core.Configuration.Server
{
    internal static class BucketConfigExtensions
    {
        public static List<NodeAdapter> GetNodes(this BucketConfig bucketConfig)
        {
            var nodeAdapters = new List<NodeAdapter>();
            var nodes = bucketConfig.Nodes;
            var nodesExt = bucketConfig.NodesExt;

            if (nodesExt == null)
                nodeAdapters.AddRange(nodes.Select(t => new NodeAdapter(t, null, bucketConfig)));
            else if (nodes.Count == nodesExt.Count)
                nodeAdapters.AddRange(nodes.Select((t, i) => new NodeAdapter(t, nodesExt[i], bucketConfig)));
            else if (nodesExt.Count < nodes.Count)
                nodeAdapters.AddRange(nodes.Select(t => new NodeAdapter(t, null, bucketConfig)));
            else if (nodesExt.Count > nodes.Count)
                for (var i = 0; i < nodesExt.Count; i++)
                {
                    var node = nodes.Count > i ? nodes[i] : null;
                    var nodeExt = nodesExt[i];
                    nodeAdapters.Add(new NodeAdapter(node, nodeExt, bucketConfig));
                }
            else
                nodeAdapters.AddRange(nodesExt.Select((t, i) => new NodeAdapter(null, nodesExt[i], bucketConfig)));

            return nodeAdapters;
        }

        public static VBucketServerMap GetBucketServerMap(this BucketConfig bucketConfig, bool useSsl)
        {
            var node = bucketConfig.GetNodes().First();
            var port = useSsl ? node.KeyValueSsl : node.KeyValue;

            return new VBucketServerMap
            {
                VBucketMap = (short[][]) bucketConfig.VBucketServerMap.VBucketMap.Clone(),
                HashAlgorithm = bucketConfig.VBucketServerMap.HashAlgorithm,
                ServerList = bucketConfig.VBucketServerMap.ServerList.Select(x =>
                    x.Replace(node.KeyValue.ToString(), port.ToString())).ToArray(),
                NumReplicas = bucketConfig.VBucketServerMap.NumReplicas,
                VBucketMapForward = (short[][]) bucketConfig.VBucketServerMap.VBucketMapForward.Clone()
            };
        }

        public static ClusterCapabilities GetClusterCapabilities(this BucketConfig config)
        {
            return new ClusterCapabilities
            {
                Capabilities = config.ClusterCapabilities,
                Version = config.ClusterCapabilitiesVersion
            };
        }

        /// <summary>
        /// Replaces the $HOST placeholder with the Uri.Host used to bootstrap. This occurs in a single-node cluster.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="bootStrapUri"></param>
        public static void ReplacePlaceholderWithBootstrapHost(this BucketConfig config, Uri bootStrapUri)
        {
            const string hostPlaceHolder = "$HOST";
            foreach (var nodesExt in config.NodesExt)
            {
                if (nodesExt.Hostname == null) nodesExt.Hostname = bootStrapUri.Host;

                if (nodesExt.Hostname.Contains(hostPlaceHolder))
                    nodesExt.Hostname = nodesExt.Hostname.Replace(hostPlaceHolder, bootStrapUri.Host);
            }

            foreach (var node in config.Nodes)
            {
                if (node.Hostname == null) node.Hostname = bootStrapUri.Host;

                if (node.Hostname.Contains(hostPlaceHolder))
                    node.Hostname = node.Hostname.Replace(hostPlaceHolder, bootStrapUri.Host);
            }

            if (config.VBucketServerMap?.ServerList != null)
            {
                var servers = config.VBucketServerMap.ServerList;
                for (var i = 0; i < servers.Length; i++)
                {
                    servers[i] = servers[i].Replace(hostPlaceHolder, bootStrapUri.Host);
                }
            }
        }
    }
}
