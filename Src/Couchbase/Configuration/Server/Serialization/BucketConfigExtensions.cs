using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.IO.Operations;

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
            else if (nodesExt.Length > nodes.Length)
            {
                for (var i = 0; i < nodesExt.Length; i++)
                {
                    var node = nodes.Length > i ? nodes[i] : null;
                    var nodeExt = nodesExt[i];
                    nodeAdapters.Add(new NodeAdapter(node, nodeExt));
                }
            }
            else
            {
                nodeAdapters.AddRange(nodesExt.Select((t, i) => new NodeAdapter(null, nodesExt[i])));
            }

            return nodeAdapters;
        }

        public static VBucketServerMap GetBucketServerMap(this IBucketConfig bucketConfig, bool useSsl)
        {
            var node = bucketConfig.GetNodes().First();
            var port = useSsl ? node.KeyValueSsl : node.KeyValue;

            return new VBucketServerMap()
            {
                VBucketMap = (int[][])bucketConfig.VBucketServerMap.VBucketMap.Clone(),
                HashAlgorithm = bucketConfig.VBucketServerMap.HashAlgorithm,
                ServerList =  bucketConfig.VBucketServerMap.ServerList.Select(x =>
                    x.Replace(node.KeyValue.ToString(), port.ToString())).ToArray(),
                NumReplicas = bucketConfig.VBucketServerMap.NumReplicas,
                VBucketMapForward =(int[][]) bucketConfig.VBucketServerMap.VBucketMapForward.Clone()
            };
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
