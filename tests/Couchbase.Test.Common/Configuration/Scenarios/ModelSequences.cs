#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Test.Common.Configuration.Model;

namespace Couchbase.Test.Common.Configuration.Scenarios
{
    /// <summary>
    /// The model events of the cluster changes in the Capella and server captures.
    /// </summary>
    /// <remarks>
    /// The later events of a sequence depend on the state, so apply each event before the next one is read.
    /// With more than one bucket, the buckets rebalance one after the other, in the order of the model.
    /// </remarks>
    internal static class ModelSequences
    {
        private const string Kv = "kv";
        private const string Query = "n1ql";

        /// <summary>
        /// A scale out like the one seen on Capella. The new nodes join with kv only, and each bucket adds them and
        /// rebalances. Then the nodes get n1ql, then the rest of their services.
        /// </summary>
        /// <param name="services">The default services of the model when null.</param>
        public static IEnumerable<ModelEvent> ScaleOut(ClusterModel model, IReadOnlyList<string> buckets, int count,
            IReadOnlyList<string>? services = null, int seed = 0)
        {
            var before = model.Cluster.Nodes.Select(n => n.Hostname).ToList();
            yield return new AddNodes(count, [Kv]);

            var added = model.Cluster.Nodes.Select(n => n.Hostname).Where(h => !before.Contains(h)).ToList();
            foreach (var bucket in buckets)
            {
                yield return new AddToBucket(bucket, added);
                foreach (var e in Rebalance(model, bucket, [], seed)) yield return e;
            }

            var target = services ?? model.DefaultServices;
            if (target.Contains(Query) && target.Any(s => s is not (Kv or Query)))
            {
                foreach (var host in added) yield return new SetServices(host, [Kv, Query]);
            }
            if (target.Any(s => s != Kv))
            {
                foreach (var host in added) yield return new SetServices(host, target);
            }
        }

        public static IEnumerable<ModelEvent> ScaleOut(ClusterModel model, string bucket, int count, int seed = 0) =>
            ScaleOut(model, [bucket], count, null, seed);

        /// <summary>
        /// A scale in like the one seen on Capella. Each bucket that holds a leaving host rebalances and drops it. Then
        /// each data host stops n1ql, then its other services, and the hosts leave nodesExt.
        /// </summary>
        public static IEnumerable<ModelEvent> ScaleIn(ClusterModel model, IReadOnlyList<string> buckets,
            IReadOnlyList<string> hosts, int seed = 0)
        {
            foreach (var bucket in buckets)
            {
                var leaving = hosts.Where(model.Bucket(bucket).IsBucketNode).ToList();
                if (leaving.Count == 0) continue;

                foreach (var e in Rebalance(model, bucket, leaving, seed)) yield return e;
                yield return new RemoveFromBucket(bucket, leaving);
            }

            foreach (var host in hosts)
            {
                if (model.Cluster.Node(host) is { } node && node.Has(Kv) && node.Has(Query) && node.Services.Any(s => s is not (Kv or Query)))
                {
                    yield return new SetServices(host, node.Services.Where(s => s != Query).OrderBy(s => s, StringComparer.Ordinal).ToList());
                }
            }
            foreach (var host in hosts)
            {
                if (model.Cluster.Node(host) is { } node && node.Has(Kv) && node.Services.Count > 1)
                {
                    yield return new SetServices(host, [Kv]);
                }
            }
            yield return new EjectNodes(hosts);
        }

        public static IEnumerable<ModelEvent> ScaleIn(ClusterModel model, string bucket, string host, int seed = 0) =>
            ScaleIn(model, [bucket], [host], seed);

        /// <summary>
        /// Each move takes 1 to 3 pending vbuckets in a seeded random order.
        /// </summary>
        public static IEnumerable<ModelEvent> Rebalance(ClusterModel model, string bucket, IReadOnlyList<string> leaving, int seed)
        {
            yield return new StartRebalance(bucket, leaving);
            var random = new Random(seed);
            while (model.PendingMoves(bucket) is { Count: > 0 } pending)
            {
                var take = random.Next(1, 4);
                yield return new MoveVBuckets(bucket, pending.OrderBy(_ => random.Next()).Take(take).OrderBy(v => v).ToList());
            }
            yield return new FinishRebalance(bucket);
        }

        /// <summary>
        /// The forward map swaps the actives off the host, the moves take 1 or 2 vbuckets each, and the failover comes
        /// once the forward map is cleared.
        /// </summary>
        public static IEnumerable<ModelEvent> GracefulFailover(ClusterModel model, string bucket, string host, int seed)
        {
            foreach (var e in MoveActivesOff(model, bucket, [host], seed)) yield return e;
            yield return new FailOver([host]);
        }

        /// <summary>
        /// A failover like the server captures. A hard failover fails the hosts over and ejects them. A graceful one
        /// first moves the actives off the hosts in each bucket that holds them. An unsafe one starts a new epoch, and
        /// each bucket then rebalances to create the vbuckets it lost.
        /// </summary>
        /// <remarks>
        /// The hard and graceful failovers also give every map a version with only a new rev, as in the captures.
        /// </remarks>
        public static IEnumerable<ModelEvent> FailOverHosts(ClusterModel model, IReadOnlyList<string> buckets,
            IReadOnlyList<string> hosts, Failover kind, int seed = 0)
        {
            switch (kind)
            {
                case Failover.Hard:
                    yield return new FailOver(hosts);
                    yield return new EjectNodes(hosts);
                    yield return new BumpAll();
                    break;
                case Failover.Graceful:
                    yield return new BumpAll();
                    foreach (var bucket in buckets)
                    {
                        var failing = hosts.Where(model.Bucket(bucket).IsBucketNode).ToList();
                        if (failing.Count == 0) continue;
                        foreach (var e in MoveActivesOff(model, bucket, failing, seed)) yield return e;
                    }
                    yield return new FailOver(hosts);
                    yield return new EjectNodes(hosts);
                    yield return new BumpAll();
                    break;
                case Failover.Unsafe:
                    yield return new BumpEpoch();
                    yield return new FailOver(hosts);
                    yield return new EjectNodes(hosts);
                    foreach (var bucket in buckets)
                    {
                        foreach (var e in Rebalance(model, bucket, [], seed)) yield return e;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        /// <summary>
        /// A failed host comes back. Full recovery brings back the node only, and each bucket adds it. Delta recovery
        /// also brings back its copies. Then each bucket rebalances.
        /// </summary>
        public static IEnumerable<ModelEvent> Recover(ClusterModel model, IReadOnlyList<string> buckets, string host,
            Recovery kind, int seed = 0)
        {
            yield return new RecoverNode(host, Delta: kind == Recovery.Delta);
            foreach (var bucket in buckets)
            {
                if (!model.Bucket(bucket).IsBucketNode(host)) yield return new AddToBucket(bucket, [host]);
                foreach (var e in Rebalance(model, bucket, [], seed)) yield return e;
            }
        }

        public static void Run(ClusterModel model, IEnumerable<ModelEvent> events, Action<ModelEvent>? after = null)
        {
            foreach (var e in events)
            {
                model.Apply(e);
                after?.Invoke(e);
            }
        }

        private static IEnumerable<ModelEvent> MoveActivesOff(ClusterModel model, string bucket, IReadOnlyList<string> hosts, int seed)
        {
            yield return new StartGracefulFailover(bucket, hosts);
            var random = new Random(seed);
            while (model.PendingMoves(bucket) is { Count: > 0 } pending)
            {
                var take = random.Next(1, 3);
                yield return new MoveVBuckets(bucket, pending.OrderBy(_ => random.Next()).Take(take).OrderBy(v => v).ToList());
            }
            yield return new FinishRebalance(bucket);
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
