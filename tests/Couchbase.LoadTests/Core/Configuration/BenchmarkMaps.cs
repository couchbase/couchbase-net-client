#nullable enable
using System;
using Couchbase.Core.Configuration.Server;
using Couchbase.Test.Common.Configuration;

namespace Couchbase.LoadTests.Core.Configuration
{
    /// <summary>
    /// The maps of the config benchmarks. Every node runs the data service, and a bucket map has 1024 vbuckets and one replica.
    /// </summary>
    internal static class BenchmarkMaps
    {
        internal const string BucketName = "bench";

        internal static string Host(int node) => $"node-{node}.example.com";

        internal static ClusterMapBuilder Global(int nodes, ulong rev, Action<int, NodeBuilder>? configure = null) =>
            WithNodes(ClusterMapBuilder.Global(), nodes, rev, configure);

        internal static ClusterMapBuilder Bucket(int nodes, ulong rev, string name = BucketName,
            Action<int, NodeBuilder>? configure = null) =>
            WithNodes(ClusterMapBuilder.Bucket(name), nodes, rev, configure);

        /// <summary>
        /// Gives the node an address and ports of its own on the external network.
        /// </summary>
        internal static void External(int node, NodeBuilder builder) =>
            builder.AlternateAddress(NetworkResolution.External, $"node-{node}.external.example.net",
                p => p.Mgmt(32091, 32191).Kv(32210, 32207));

        /// <summary>
        /// Parses the map like <see cref="ClusterMapBuilder.Build"/>, then sets the network the way a fetch does on arrival.
        /// </summary>
        internal static BucketConfig Arrived(ClusterMapBuilder map, ClusterOptions options)
        {
            var config = map.Build();
            config.NetworkResolution = options.EffectiveNetworkResolution;
            return config;
        }

        private static ClusterMapBuilder WithNodes(ClusterMapBuilder map, int nodes, ulong rev,
            Action<int, NodeBuilder>? configure)
        {
            map.Rev(rev);
            for (var i = 0; i < nodes; i++)
            {
                var node = i;
                map.Node(Host(node), n =>
                {
                    n.Uuid($"node-uuid-{node}").Kv();
                    configure?.Invoke(node, n);
                });
            }
            return map;
        }
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
