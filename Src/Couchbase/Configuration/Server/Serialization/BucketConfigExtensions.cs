using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;

namespace Couchbase.Configuration.Server.Serialization
{
    internal static class BucketConfigExtensions
    {
        public static List<INodeAdapter> GetNodes(this IBucketConfig bucketConfig)
        {
            var nodeAdapters = new List<INodeAdapter>();
            var nodes = bucketConfig.Nodes.ReorderToServerList(bucketConfig.VBucketServerMap);
            var nodesExt = bucketConfig.NodesExt.ReorderToServerList(bucketConfig.VBucketServerMap);

            if (nodesExt == null)
            {
                nodeAdapters.AddRange(nodes.Select(t => new NodeAdapter(t, null)));
            }
            else if (nodes.Length == nodesExt.Length)
            {
                nodeAdapters.AddRange(nodes.Select((t, i) => new NodeAdapter(t, nodesExt[i])));
            }
            else if (nodesExt.Length < nodes.Length)
            {
                nodeAdapters.AddRange(nodes.Select(t => new NodeAdapter(t, null)));
            }
            else
            {
                nodeAdapters.AddRange(nodesExt.Select((t, i) => new NodeAdapter(null, nodesExt[i])));
            }

            return nodeAdapters;
        }
    }
}
