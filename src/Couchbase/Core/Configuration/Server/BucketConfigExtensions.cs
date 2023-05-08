#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Management.Analytics;
using Couchbase.Utils;

namespace Couchbase.Core.Configuration.Server
{
    internal static class BucketConfigExtensions
    {
        public static bool HasVBucketMapChanged(this BucketConfig config, BucketConfig? other)
        {
            if (other == null)
            {
                return true;
            }
            return !Equals(config.VBucketServerMap, other.VBucketServerMap);
        }

        public static bool HasClusterNodesChanged(this BucketConfig config, BucketConfig? other)
        {
            if (other == null)
            {
                return true;
            }
            return !(config.NodesExt.AreEqual(other.NodesExt) && config.Nodes.AreEqual(other.Nodes));
        }
        public static bool HasConfigChanges(this BucketConfig newConfig, BucketConfig? oldConfig, string bucketName)
        {
            //Wrong bucket name - configs are broadcast so keep on going
            if (!newConfig.Name.Equals(bucketName))
            {
                return false;
            }

            //first config received matches the bucket name
            if (oldConfig == null)
            {
                return true;
            }

            //There has been a config change and the newConfig may need to be loaded
            if (!newConfig.Equals(oldConfig))
            {
                return newConfig.IsNewerThan(oldConfig);
            }

            return false;
        }
        public static bool IsNewerThan(this BucketConfig newConfig, BucketConfig? oldConfig)
        {
            if (newConfig.IgnoreRev) return true; //in this case, its a DNS SRV refresh so ignore and just accept the config
            if (oldConfig == null) return true; //true we don't have a config yet so this is the new one
            if (ReferenceEquals(oldConfig, newConfig)) return false; // shouldn't happen

            if (newConfig.RevEpoch > oldConfig.RevEpoch) //new one is older than the current
                return true;
            if(newConfig.RevEpoch < oldConfig.RevEpoch) //new one is newer
            {
                return false;
            }

            return newConfig.Rev > oldConfig.Rev; //fall back for older behavior
        }

        /// <summary>
        /// This method generates a list of <see cref="HostEndpointWithPort"/>'s that can be used
        /// for bootstrapping buckets.
        /// </summary>
        /// <param name="bucketConfig">The <see cref="BucketConfig"/> reference which was returned from the server.</param>
        /// <param name="enableTls">If true, use TLS ports.</param>
        /// <returns>A list of <see cref="HostEndpointWithPort"/> for bootstrapping.</returns>
        public static IEnumerable<HostEndpointWithPort> GetBootstrapEndpoints(this BucketConfig bucketConfig, bool? enableTls)
        {
            var endpoints = new List<HostEndpointWithPort>();
            foreach (var nodeAdapter in bucketConfig.GetNodes())
            {
                if (nodeAdapter.IsKvNode)
                {
                    endpoints.Add(new HostEndpointWithPort(nodeAdapter.Hostname,
                        enableTls.GetValueOrDefault(false) ?
                        nodeAdapter.KeyValueSsl : nodeAdapter.KeyValue));
                }
            }

            return endpoints;
        }

        public static List<NodeAdapter> GetNodes(this BucketConfig bucketConfig)
        {
            var nodeAdapters = new List<NodeAdapter>();
            var nodes = bucketConfig.Nodes;
            var nodesExt = bucketConfig.NodesExt;

            var nodesCount = nodes?.Count;
            var nodesExtCount = nodesExt?.Count;

            if (nodesExt == null)
                nodeAdapters.AddRange(nodes?.Select(t => new NodeAdapter(t, null, bucketConfig)) ?? Enumerable.Empty<NodeAdapter>());
            else if (nodes?.Count == nodesExt.Count)
                nodeAdapters.AddRange(nodes!.Select((t, i) => new NodeAdapter(t, nodesExt[i], bucketConfig)));
            else if (nodesExt.Count < nodes?.Count)
                nodeAdapters.AddRange(nodes!.Select(t => new NodeAdapter(t, null, bucketConfig)));
            else if (nodesExt.Count > nodes?.Count && nodes?.Count > 0)
            {
                //In certain cases the server will return an evicted node at the end of the NodesExt list
                //we filter that list to ensure parity between Nodes and NodesExt - see NCBC-2422 for details
                var nodesExtFiltered = nodesExt.Where(ext => nodes.Count == 0
                                                             || ext.Services.Kv <= 0
                                                             || nodes.Any(n => n.Hostname.Contains(ext.Hostname)));

                //create the adapters list - if node exists, it should pair with its equiv nodeExt
                foreach (var ext in nodesExtFiltered)
                {
                    var node = nodes.FirstOrDefault(x => x.Hostname.Contains(ext.Hostname));
                    nodeAdapters.Add(new NodeAdapter(node, ext, bucketConfig));
                }
            }
            else
                nodeAdapters.AddRange(nodesExt.Select((t, i) => new NodeAdapter(null, nodesExt[i], bucketConfig)));

            if (nodeAdapters.Count == 0)
            {
                throw new ServiceMissingException(
                    $"No node adapters selected.  nodes = {nodesCount}, nodesExt = {nodesExtCount}");
            }

            return nodeAdapters;
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
        /// <param name="config">Configuration to update.</param>
        /// <param name="host">New host value for $HOST.</param>
        public static void ReplacePlaceholderWithBootstrapHost(this BucketConfig config, string host)
        {
            const string hostPlaceHolder = "$HOST";
            foreach (var nodesExt in config.NodesExt)
            {
                if (nodesExt.Hostname == null) nodesExt.Hostname = host;

                if (nodesExt.Hostname.Contains(hostPlaceHolder))
                    nodesExt.Hostname = nodesExt.Hostname.Replace(hostPlaceHolder, host);
            }

            if (config.Nodes != null)
            {
                foreach (var node in config.Nodes)
                {
                    if (node.Hostname == null) node.Hostname = host;

                    if (node.Hostname.Contains(hostPlaceHolder))
                        node.Hostname = node.Hostname.Replace(hostPlaceHolder, host);
                }
            }

            if (config.VBucketServerMap?.ServerList != null)
            {
                var servers = config.VBucketServerMap.ServerList;
                for (var i = 0; i < servers.Length; i++)
                {
                    servers[i] = servers[i].Replace(hostPlaceHolder, host);
                }
            }
        }
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
