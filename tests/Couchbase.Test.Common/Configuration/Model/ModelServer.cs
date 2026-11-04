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
    /// A node that leaves the cluster calls <see cref="FakeClusterServer.Leave"/> before its next delivery. Read
    /// <see cref="Leave"/> for when a node leaves.
    /// </remarks>
    internal sealed class ModelServer(ClusterModel model, FakeClusterServer server)
    {
        private readonly Dictionary<(string Host, string Map), Delivery> _served = new();
        private readonly HashSet<string> _departed = new(StringComparer.Ordinal);
        private readonly HashSet<string> _toLeave = new(StringComparer.Ordinal);
        private Dictionary<string, Place> _places = Places(model, []);

        /// <summary>
        /// Whether a node that leaves the cluster calls <see cref="FakeClusterServer.Leave"/>. True by default. A test that
        /// looks at something else can turn it off, as the SDK can then drop the version that removes the node.
        /// </summary>
        public bool NodesLeave { get; set; } = true;

        /// <summary>
        /// The node serves the latest version of every map, and notifies its connections of the maps that changed for it.
        /// A node that left the cluster leaves the fake server first.
        /// </summary>
        /// <param name="notify">False to only serve the maps, so the connections learn of them on their next fetch.</param>
        public void Deliver(string host, bool notify = true)
        {
            Leave(h => h == host);
            Deliver(host, seed: false, notify);
        }

        /// <summary>
        /// Delivers to every node of the latest cluster map. A node that served a cluster map with itself in it, and is
        /// not in the latest one, gets one more delivery. So an ejected node learns that it left and serves bodies
        /// without itself. The nodes that left the cluster leave the fake server first.
        /// </summary>
        /// <param name="notify">False to only serve the maps, like <see cref="Deliver"/>.</param>
        /// <param name="except">Hosts that get no delivery, so they keep serving the versions they served.</param>
        public void DeliverAll(bool notify = true, IReadOnlyCollection<string>? except = null)
        {
            Leave(except);
            var hosts = model.Cluster.Nodes.Select(n => n.Hostname).ToList();
            hosts.AddRange(_served
                .Where(s => s.Key.Map == BucketConfig.GlobalBucketName && s.Value.Map.Node(s.Key.Host) is not null)
                .Select(s => s.Key.Host)
                .Where(h => !hosts.Contains(h))
                .OrderBy(h => h, StringComparer.Ordinal)
                .ToList());
            foreach (var host in hosts)
            {
                if (except?.Contains(host) == true) continue;
                Deliver(host, seed: false, notify);
            }
        }

        /// <summary>
        /// Makes each node that left the cluster since its last delivery leave the fake server, with the select bucket
        /// status of the profile. <see cref="DeliverAll"/> and <see cref="Deliver"/> call it first, so a caller calls it
        /// only to act between the leave and the delivery.
        /// </summary>
        /// <remarks>
        /// A data node leaves with its last bucket, and not by a failover. The server deletes the bucket on the node, and
        /// the captures show the node ends its connections a few ms before the map without it in the bucket reaches the
        /// SDK. A failed node keeps its connections until the rebalance that forgets it. Any other node leaves with
        /// nodesExt. A node that lags leaves when it gets a delivery again.
        /// With several buckets the node leaves with the last one. A real node also ends the connections of each bucket
        /// that leaves it before.
        /// </remarks>
        /// <param name="except">Hosts that get no delivery now, like in <see cref="DeliverAll"/>.</param>
        /// <returns>The hosts that left, in ordinal order.</returns>
        public IReadOnlyList<string> Leave(IReadOnlyCollection<string>? except = null) =>
            Leave(h => except?.Contains(h) != true);

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
                Deliver(node.Hostname, seed: true, notify: false);
            }
        }

        /// <summary>
        /// The version a node serves for a bucket, or for the cluster map when the bucket is null. Null before the first
        /// delivery to the node.
        /// </summary>
        public ConfigVersion? Served(string host, string? bucket) =>
            _served.TryGetValue((host, bucket ?? BucketConfig.GlobalBucketName), out var delivery) ? delivery.Map.Version : null;

        private List<string> Leave(Func<string, bool> delivered)
        {
            FindDepartures();
            if (!NodesLeave) _toLeave.Clear();
            var leaving = _toLeave.Where(delivered).OrderBy(h => h, StringComparer.Ordinal).ToList();
            foreach (var host in leaving)
            {
                _toLeave.Remove(host);
                server.Leave(host, model.Profile.LeftNodeSelectBucketStatus);
            }
            return leaving;
        }

        // It compares where each host is in the model now with where it was at the last call.
        private void FindDepartures()
        {
            var places = Places(model, _places.Keys);
            foreach (var pair in places)
            {
                var (host, now) = (pair.Key, pair.Value);
                if (!_places.TryGetValue(host, out var before)) continue;

                if (now.InCluster && !before.InCluster)
                {
                    _departed.Remove(host);
                    _toLeave.Remove(host);
                }
                if (Departs(before, now) && _departed.Add(host)) _toLeave.Add(host);
            }
            _places = places;
        }

        // Its last bucket drops it, a rebalance forgets it after a failover, or it leaves nodesExt while in no bucket.
        private static bool Departs(Place before, Place now) =>
            !now.Failed
            && ((before.InBucket && !now.InBucket)
                || (before.Failed && !now.InCluster)
                || (before.InCluster && !now.InCluster && !before.Failed));

        private static Dictionary<string, Place> Places(ClusterModel model, IEnumerable<string> known)
        {
            var failed = model.FailedHosts;
            return model.Cluster.Nodes.Select(n => n.Hostname).Concat(failed).Concat(known)
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(h => h, h => new Place(
                    model.Cluster.Node(h) is not null,
                    model.Buckets.Any(b => model.Bucket(b).IsBucketNode(h)),
                    failed.Contains(h)), StringComparer.Ordinal);
        }

        private void Deliver(string host, bool seed, bool notify)
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
                server.Serve(host, delivery.Body, notify);
            }
        }

        /// <summary>
        /// Where a host is in the model at one point.
        /// </summary>
        private readonly record struct Place(bool InCluster, bool InBucket, bool Failed);

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
