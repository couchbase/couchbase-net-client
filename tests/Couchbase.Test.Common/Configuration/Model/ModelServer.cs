#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.Test.Common.Configuration.Model
{
    /// <summary>
    /// Makes the nodes of a <see cref="FakeClusterServer"/> serve the maps of a <see cref="ClusterModel"/>. Each node
    /// serves the maps rendered for itself, so only its own entry has thisNode.
    /// </summary>
    /// <remarks>
    /// A node serves the versions of its last delivery. To make a node lag, deliver to it later than to the others.
    /// </remarks>
    internal sealed class ModelServer(ClusterModel model, FakeClusterServer server)
    {
        private readonly Dictionary<(string Host, string Map), Delivery> _served = new();

        /// <summary>
        /// The node serves the latest version of every map, and notifies its connections of the maps that changed for it.
        /// </summary>
        public void Deliver(string host) => Deliver(host, seed: false);

        /// <summary>
        /// Delivers to every node of the latest cluster map. A node that served a cluster map with itself in it, and is
        /// not in the latest one, gets one more delivery. So an ejected node learns that it left and serves bodies
        /// without itself.
        /// </summary>
        public void DeliverAll()
        {
            var hosts = model.Cluster.Nodes.Select(n => n.Hostname).ToList();
            hosts.AddRange(_served
                .Where(s => s.Key.Map == BucketConfig.GlobalBucketName && s.Value.Map.Node(s.Key.Host) is not null)
                .Select(s => s.Key.Host)
                .Where(h => !hosts.Contains(h))
                .OrderBy(h => h, StringComparer.Ordinal)
                .ToList());
            foreach (var host in hosts)
            {
                Deliver(host, seed: false);
            }
        }

        /// <summary>
        /// Every node of the latest cluster map serves the latest version of every map, with no notification.
        /// </summary>
        /// <remarks>
        /// Call it after <see cref="ConfigHarness.Create"/> and <see cref="ConfigHarness.OpenBucketAsync"/>. Both make every
        /// node serve one map, and OpenBucketAsync drops what each node served for that bucket.
        /// </remarks>
        public void Seed()
        {
            foreach (var node in model.Cluster.Nodes)
            {
                Deliver(node.Hostname, seed: true);
            }
        }

        /// <summary>
        /// The version a node serves for a bucket, or for the cluster map when the bucket is null. Null before the first
        /// delivery to the node.
        /// </summary>
        public ConfigVersion? Served(string host, string? bucket) =>
            _served.TryGetValue((host, bucket ?? BucketConfig.GlobalBucketName), out var delivery) ? delivery.Map.Version : null;

        private void Deliver(string host, bool seed)
        {
            foreach (var map in model.Buckets.Select(model.Bucket).Prepend(model.Cluster))
            {
                var name = map.Bucket ?? BucketConfig.GlobalBucketName;
                var key = (host, name);
                if (_served.TryGetValue(key, out var delivery) && delivery.Map.Version == map.Version)
                {
                    if (seed) server.Serve(host, delivery.Body, notify: false);
                    continue;
                }

                var body = model.Render(map, host).ToUtf8Bytes();
                delivery = new Delivery(map, new FakeClusterServer.ServedMap(name, body, map.Version));
                _served[key] = delivery;
                server.Serve(host, delivery.Body, notify: !seed);
            }
        }

        private sealed class Delivery(ModelMap map, FakeClusterServer.ServedMap body)
        {
            public ModelMap Map { get; } = map;

            public FakeClusterServer.ServedMap Body { get; } = body;
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
