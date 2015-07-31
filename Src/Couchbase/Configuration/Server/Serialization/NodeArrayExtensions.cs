using System;
using System.Linq;
using Couchbase.Core;

namespace Couchbase.Configuration.Server.Serialization
{
    public static class NodeArrayExtensions
    {
        public static bool AreEqual(this Node[] nodes, Node[] other)
        {
            if (nodes == null && other == null)
            {
                return true;
            }
            if (nodes != null && nodes.Length != other.Length)
            {
                return false;
            }
            if (nodes == null) return false;
            var ordered = nodes.OrderBy(x => x.Hostname).ToArray();
            var orderedOther = other.OrderBy(x => x.Hostname).ToArray();
            for (var i = 0; i < ordered.Count(); i++)
            {
                if (string.Compare(ordered[i].Hostname,
                    orderedOther[i].Hostname,
                    StringComparison.OrdinalIgnoreCase) != 0)
                {
                    return false;
                }
            }
            return true;
        }

        public static Node[] ReorderToServerList(this Node[] nodes, VBucketServerMap serverMap)
        {
            var reordered = new Node[nodes.Length];
            var serversList = serverMap.ServerList;

            if (serversList == null || serversList.Length == 0)
            {
                reordered = nodes;
            }
            else
            {
                for (var i = 0; i < serversList.Length; i++)
                {
                    var host = serversList[i].Split(':')[0];
                    foreach (var n in nodes.Where(n => n.Hostname.Split(':')[0].Equals(host)))
                    {
                        reordered[i] = n;
                        break;
                    }
                }
            }
            return reordered;
        }

        public static NodeExt[] ReorderToServerList(this NodeExt[] nodes, VBucketServerMap serverMap)
        {
            if (nodes == null) return null;
            var reordered = new NodeExt[nodes.Length];
            var serversList = serverMap.ServerList;

            if (serversList == null || serversList.Length == 0)
            {
                reordered = nodes;
            }
            else
            {
                for (var i = 0; i < serversList.Length; i++)
                {
                    var host = serversList[i].Split(':')[0];
                    foreach (var n in nodes.Where(n => n.Hostname != null
                        && n.Hostname.Split(':')[0].Equals(host)))
                    {
                        reordered[i] = n;
                        break;
                    }
                }
                for (var i = 0; i < nodes.Length; i++)
                {
                    var cur = nodes[i];
                    if (!reordered.Contains(cur))
                    {
                        reordered[i] = cur;
                    }
                }
            }
            return reordered;
        }
    }
}
