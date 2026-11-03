#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.Test.Common.Configuration.Model
{
    /// <summary>
    /// The state of a cluster over time. It applies <see cref="ModelEvent"/>s, keeps every version of every map and
    /// renders the view of each node through <see cref="ClusterMapBuilder"/>.
    /// </summary>
    /// <remarks>
    /// The cluster has one rev counter, like the server. A change to the nodes or their services gives the cluster map
    /// and every bucket map a new version with the same rev. A change to one bucket gives only that bucket a new version.
    /// A scale out like the one seen on Capella.
    /// <code>
    /// var model = ClusterModel.Create(ClusterProfile.Load(ClusterProfile.CapellaPrivateLink));
    /// model.Apply(new AddNodes(2, ["kv"]));
    /// model.Apply(new AddToBucket("recorder", ["node-3.example.com", "node-4.example.com"]));
    /// model.Apply(new StartRebalance("recorder", []));
    /// while (model.PendingMoves("recorder") is { Count: > 0 } pending) model.Apply(new MoveVBuckets("recorder", pending.Take(2).ToList()));
    /// model.Apply(new FinishRebalance("recorder"));
    /// </code>
    /// The same seed and the same events give the same JSON for every version.
    /// </remarks>
    internal sealed class ClusterModel
    {
        private const string Kv = "kv";

        // NodeBuilder.Services adds these by itself.
        private static readonly string[] ImpliedServices = ["mgmt", "capi", "projector"];

        private readonly ulong _epoch;
        private readonly int _vbuckets;
        private readonly int _replicas;
        private readonly HashSet<string> _knownServices;
        private readonly List<ModelMap> _cluster = [];
        private readonly Dictionary<string, List<ModelMap>> _buckets = new(StringComparer.Ordinal);
        private readonly List<(ModelEvent Event, ulong Rev)> _applied = [];
        private readonly List<string> _trace = [];
        private ulong _rev;
        private int _nextMember;

        private ClusterModel(ClusterProfile profile, IReadOnlyList<string> buckets, int seed, ulong epoch, ulong rev)
        {
            Profile = profile;
            Buckets = buckets;
            Seed = seed;
            _epoch = epoch;
            _rev = rev;

            var template = ClusterMapBuilder.Bucket(profile.BucketName, profile);
            _vbuckets = template.VBucketCount;
            _replicas = template.Replicas;
            _knownServices = new HashSet<string>(profile.NodeShape.Ports.Keys.Select(ProfileNodeShape.ServiceOf), StringComparer.Ordinal);
            DefaultServices = profile.NodeShape.ServiceSets.Count > 0
                ? profile.NodeShape.ServiceSets[0].Services.Where(s => !ImpliedServices.Contains(s)).ToList()
                : [Kv];
        }

        public ClusterProfile Profile { get; }

        public IReadOnlyList<string> Buckets { get; }

        public int Seed { get; }

        /// <summary>
        /// The services of the first service set of the profile, without the ones <see cref="NodeBuilder.Services"/> adds.
        /// Nodes get them unless an event names other services.
        /// </summary>
        public IReadOnlyList<string> DefaultServices { get; }

        /// <summary>
        /// The latest cluster map.
        /// </summary>
        public ModelMap Cluster => _cluster[_cluster.Count - 1];

        /// <summary>
        /// Every applied event with the rev of its commit, in order.
        /// </summary>
        public IReadOnlyList<(ModelEvent Event, ulong Rev)> Applied => _applied;

        /// <summary>
        /// A model of <paramref name="nodes"/> members with the default services. Every node is in every bucket, and each
        /// bucket has a balanced map. Every map starts at version (<paramref name="epoch"/>, <paramref name="rev"/>).
        /// </summary>
        /// <param name="buckets">The bucket of the profile when null.</param>
        public static ClusterModel Create(ClusterProfile profile, int nodes = 3, IReadOnlyList<string>? buckets = null,
            int seed = 0, ulong epoch = 1, ulong rev = 1)
        {
            if (nodes < 1) throw new ArgumentOutOfRangeException(nameof(nodes), nodes, "A cluster has at least one node.");
            buckets ??= [profile.BucketName];
            if (buckets.Distinct(StringComparer.Ordinal).Count() != buckets.Count)
            {
                throw new ArgumentException("Bucket names must be distinct.", nameof(buckets));
            }

            var model = new ClusterModel(profile, buckets.ToList(), seed, epoch, rev);
            if (buckets.Count > 0 && !model.DefaultServices.Contains(Kv))
            {
                throw new ArgumentException($"The first service set of profile {profile.Name} has no kv, so no node can hold a bucket.", nameof(profile));
            }

            var version = new ConfigVersion(epoch, rev);
            var members = Enumerable.Range(0, nodes).Select(m => model.NewNode(Hostname(m), m, model.DefaultServices)).ToList();
            model._cluster.Add(ModelMap.ForCluster(version, members));
            model._nextMember = nodes;

            var hosts = members.Select(n => n.Hostname).ToList();
            var groups = members.Select(n => n.ServerGroup).ToList();
            var empty = Enumerable.Range(0, model._vbuckets).Select(_ => Enumerable.Repeat((short)-1, model._replicas + 1).ToArray()).ToArray();
            foreach (var bucket in buckets)
            {
                var chains = VBucketPlanner.Plan(empty, Enumerable.Range(0, nodes).ToList(), groups, model._replicas, model.PlanRandom(bucket, rev));
                model._buckets[bucket] = [ModelMap.ForBucket(bucket, version, members, hosts, chains, null, model._replicas)];
            }
            return model;
        }

        /// <summary>
        /// The latest map of a bucket.
        /// </summary>
        public ModelMap Bucket(string name) => BucketHistory(name)[BucketHistory(name).Count - 1];

        /// <summary>
        /// Every version of one map, oldest first. A null bucket is the cluster map.
        /// </summary>
        public IReadOnlyList<ModelMap> History(string? bucket) => bucket is null ? _cluster : BucketHistory(bucket);

        /// <summary>
        /// The vbuckets whose chain is not yet its forward chain, in ascending order. Empty when no rebalance is running.
        /// </summary>
        public IReadOnlyList<int> PendingMoves(string bucket) => Pending(Bucket(bucket));

        /// <summary>
        /// Applies one event as one commit, and returns the new versions, the cluster map first.
        /// </summary>
        /// <exception cref="InvalidOperationException">The event does not fit the state. The state does not change.</exception>
        public IReadOnlyList<ModelMap> Apply(ModelEvent e)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));

            // Each handler checks the event and builds the new maps before anything changes.
            var version = new ConfigVersion(_epoch, _rev + 1);
            var changed = e switch
            {
                AddNodes add => Apply(add, version),
                RejoinNode rejoin => Apply(rejoin, version),
                SetServices services => Apply(services, version),
                AddToBucket add => Apply(add, version),
                StartRebalance start => Apply(start, version),
                MoveVBuckets move => Apply(move, version),
                FinishRebalance finish => Apply(finish, version),
                RemoveFromBucket remove => Apply(remove, version),
                EjectNodes eject => Apply(eject, version),
                BumpRev bump => Apply(bump, version),
                _ => throw new ArgumentException($"The model has no rule for {e.GetType().Name}.", nameof(e))
            };

            foreach (var map in changed)
            {
                (map.Bucket is null ? _cluster : _buckets[map.Bucket]).Add(map);
            }
            _rev = version.Revision;
            _nextMember = Math.Max(_nextMember, Cluster.Nodes.Select(n => n.Member + 1).DefaultIfEmpty(0).Max());
            _applied.Add((e, _rev));
            _trace.Add($"{_rev} {e} -> {string.Join(", ", changed.Select(m => m.Bucket ?? "cluster"))}");
            return changed;
        }

        /// <summary>
        /// One line per applied event, with its rev and the maps it changed.
        /// </summary>
        public string Trace() => string.Join("\n", _trace);

        /// <summary>
        /// The map as <paramref name="servingHost"/> serves it. Only the entry of that host has thisNode, and no entry has
        /// it when the host is null or not in the map.
        /// </summary>
        public ClusterMapBuilder Render(ModelMap map, string? servingHost)
        {
            var builder = map.Bucket is null ? ClusterMapBuilder.Global(Profile) : ClusterMapBuilder.Bucket(map.Bucket, Profile);
            builder.Rev(map.Version.Revision).Epoch(map.Version.Epoch);
            foreach (var node in map.Nodes)
            {
                // The builder lists nodes outside the bucket last, in the order they were added.
                var outside = map.Bucket is not null && !map.IsBucketNode(node.Hostname);
                builder.Node(node.Hostname, node.Member, n =>
                {
                    n.Services(node.Services.ToArray()).Uuid(node.NodeUuid);
                    if (node.ServerGroup is not null) n.ServerGroup(node.ServerGroup);
                    if (outside) n.OutsideBucket();
                    if (node.Hostname == servingHost) n.ThisNode();
                });
            }

            if (map.Bucket is not null)
            {
                builder.VBucketMap(map.Chains);
                if (map.ForwardChains is { } forward) builder.ForwardMap(forward);
            }
            return builder;
        }

        private List<ModelMap> Apply(AddNodes e, ConfigVersion version)
        {
            if (e.Count < 1) throw Rejected(e, "The count must be 1 or more.");
            var services = Checked(e, e.Services ?? DefaultServices);
            var nodes = Cluster.Nodes.ToList();
            for (var member = _nextMember; member < _nextMember + e.Count; member++)
            {
                nodes.Add(NewNode(Hostname(member), member, services));
            }
            return WithClusterNodes(nodes, version);
        }

        private List<ModelMap> Apply(RejoinNode e, ConfigVersion version)
        {
            if (Cluster.Node(e.Host) is not null) throw Rejected(e, $"{e.Host} is in nodesExt.");
            if (!_cluster.Any(m => m.Node(e.Host) is not null))
            {
                throw Rejected(e, $"{e.Host} was never in the cluster. AddNodes adds new hosts.");
            }

            var services = Checked(e, e.Services ?? DefaultServices);
            return WithClusterNodes([.. Cluster.Nodes, NewNode(e.Host, _nextMember, services)], version);
        }

        private List<ModelMap> Apply(SetServices e, ConfigVersion version)
        {
            if (Cluster.Node(e.Host) is null) throw Rejected(e, $"{e.Host} is not in nodesExt.");
            var services = Checked(e, e.Services);
            if (!services.Contains(Kv) && Buckets.FirstOrDefault(b => Bucket(b).IsBucketNode(e.Host)) is { } bucket)
            {
                throw Rejected(e, $"{e.Host} is in bucket {bucket}, so it keeps kv.");
            }

            return WithClusterNodes(Cluster.Nodes.Select(n => n.Hostname == e.Host ? n.WithServices(services) : n).ToList(), version);
        }

        private List<ModelMap> Apply(AddToBucket e, ConfigVersion version)
        {
            var map = BucketMap(e, e.Bucket);
            CheckHosts(e, e.Hosts);
            foreach (var host in e.Hosts)
            {
                var node = Cluster.Node(host) ?? throw Rejected(e, $"{host} is not in nodesExt.");
                if (!node.Has(Kv)) throw Rejected(e, $"{host} has no kv.");
                if (map.IsBucketNode(host)) throw Rejected(e, $"{host} is in the bucket already.");
            }

            return [NextBucket(map, version, [.. map.BucketNodes, .. e.Hosts], map.Chains, map.ForwardChains)];
        }

        private List<ModelMap> Apply(StartRebalance e, ConfigVersion version)
        {
            var map = BucketMap(e, e.Bucket);
            if (map.ForwardChains is not null) throw Rejected(e, "A rebalance is running.");
            CheckHosts(e, e.Leaving, allowEmpty: true);
            if (e.Leaving.FirstOrDefault(h => !map.IsBucketNode(h)) is { } stranger) throw Rejected(e, $"{stranger} is not in the bucket.");

            var target = Enumerable.Range(0, map.BucketNodes.Count).Where(i => !e.Leaving.Contains(map.BucketNodes[i])).ToList();
            if (target.Count == 0) throw Rejected(e, "No node would hold the vbuckets.");
            var groups = map.BucketNodes.Select(h => Cluster.Node(h)!.ServerGroup).ToList();
            var forward = VBucketPlanner.Plan(map.Chains, target, groups, _replicas, PlanRandom(e.Bucket, version.Revision));
            for (var vbucket = 0; vbucket < forward.Length; vbucket++)
            {
                if (SameChain(forward[vbucket], map.Chains[vbucket])) forward[vbucket] = map.Chains[vbucket];
            }

            return [NextBucket(map, version, map.BucketNodes, map.Chains, forward)];
        }

        private List<ModelMap> Apply(MoveVBuckets e, ConfigVersion version)
        {
            var map = BucketMap(e, e.Bucket);
            var forward = map.ForwardChains ?? throw Rejected(e, "No rebalance is running.");
            if (e.VBuckets.Count == 0) throw Rejected(e, "The event lists no vbucket.");
            if (e.VBuckets.Distinct().Count() != e.VBuckets.Count) throw Rejected(e, "The event lists a vbucket twice.");

            var chains = (short[][])map.Chains.Clone();
            foreach (var vbucket in e.VBuckets)
            {
                if (vbucket < 0 || vbucket >= chains.Length) throw Rejected(e, $"The bucket has no vbucket {vbucket}.");
                if (SameChain(chains[vbucket], forward[vbucket])) throw Rejected(e, $"vbucket {vbucket} is not pending.");
                chains[vbucket] = forward[vbucket];
            }

            return [NextBucket(map, version, map.BucketNodes, chains, forward)];
        }

        private List<ModelMap> Apply(FinishRebalance e, ConfigVersion version)
        {
            var map = BucketMap(e, e.Bucket);
            if (map.ForwardChains is null) throw Rejected(e, "No rebalance is running.");
            var pending = Pending(map);
            if (pending.Count > 0) throw Rejected(e, $"{pending.Count} vbuckets are pending, the first is {pending[0]}.");

            return [NextBucket(map, version, map.BucketNodes, map.Chains, null)];
        }

        private List<ModelMap> Apply(RemoveFromBucket e, ConfigVersion version)
        {
            var map = BucketMap(e, e.Bucket);
            CheckHosts(e, e.Hosts);
            var newIndex = new short[map.BucketNodes.Count];
            for (var i = 0; i < newIndex.Length; i++) newIndex[i] = (short)i;
            foreach (var host in e.Hosts)
            {
                var index = map.BucketNodes.ToList().IndexOf(host);
                if (index < 0) throw Rejected(e, $"{host} is not in the bucket.");
                if (FirstHolding(map.Chains, index) is { } vbucket) throw Rejected(e, $"{host} holds a copy of vbucket {vbucket}.");
                if (map.ForwardChains is { } forward && FirstHolding(forward, index) is { } next)
                {
                    throw Rejected(e, $"{host} holds a copy of vbucket {next} in the forward map.");
                }
                newIndex[index] = -1;
            }

            short kept = 0;
            for (var i = 0; i < newIndex.Length; i++)
            {
                if (newIndex[i] >= 0) newIndex[i] = kept++;
            }

            var chains = map.Chains.Select(c => Reindexed(c, newIndex)).ToArray();
            var forwardChains = map.ForwardChains?.Select((c, v) => ReferenceEquals(c, map.Chains[v]) ? chains[v] : Reindexed(c, newIndex)).ToArray();
            var bucketNodes = map.BucketNodes.Where(h => !e.Hosts.Contains(h)).ToList();
            return [NextBucket(map, version, bucketNodes, chains, forwardChains)];
        }

        private List<ModelMap> Apply(EjectNodes e, ConfigVersion version)
        {
            CheckHosts(e, e.Hosts);
            foreach (var host in e.Hosts)
            {
                if (Cluster.Node(host) is null) throw Rejected(e, $"{host} is not in nodesExt.");
                if (Buckets.FirstOrDefault(b => Bucket(b).IsBucketNode(host)) is { } bucket) throw Rejected(e, $"{host} is in bucket {bucket}.");
            }
            if (e.Hosts.Count == Cluster.Nodes.Count) throw Rejected(e, "The cluster keeps at least one node.");

            return WithClusterNodes(Cluster.Nodes.Where(n => !e.Hosts.Contains(n.Hostname)).ToList(), version);
        }

        private List<ModelMap> Apply(BumpRev e, ConfigVersion version)
        {
            if (e.Bucket is null) return [ModelMap.ForCluster(version, Cluster.Nodes)];
            var map = BucketMap(e, e.Bucket);
            return [NextBucket(map, version, map.BucketNodes, map.Chains, map.ForwardChains)];
        }

        private List<ModelMap> WithClusterNodes(IReadOnlyList<ModelNode> nodes, ConfigVersion version)
        {
            var maps = new List<ModelMap> { ModelMap.ForCluster(version, nodes) };
            foreach (var bucket in Buckets)
            {
                var map = Bucket(bucket);
                maps.Add(ModelMap.ForBucket(bucket, version, nodes, map.BucketNodes, map.Chains, map.ForwardChains, _replicas));
            }
            return maps;
        }

        private ModelMap NextBucket(ModelMap map, ConfigVersion version, IReadOnlyList<string> bucketNodes, short[][] chains,
            short[][]? forward) =>
            ModelMap.ForBucket(map.Bucket!, version, Cluster.Nodes, bucketNodes, chains, forward, _replicas);

        private ModelNode NewNode(string hostname, int member, IReadOnlyList<string> services) =>
            new(hostname, Profile.NodeShape.NodeUuid(member), member, Profile.NodeShape.ServerGroup(member), services);

        private ModelMap BucketMap(ModelEvent e, string bucket) =>
            _buckets.ContainsKey(bucket) ? Bucket(bucket) : throw Rejected(e, $"The model has no bucket {bucket}.");

        private List<ModelMap> BucketHistory(string bucket) =>
            _buckets.TryGetValue(bucket, out var history)
                ? history
                : throw new ArgumentException($"The model has no bucket {bucket}. It has {string.Join(", ", Buckets)}.", nameof(bucket));

        private IReadOnlyList<string> Checked(ModelEvent e, IReadOnlyList<string> services)
        {
            var unknown = services.Where(s => !_knownServices.Contains(s)).ToList();
            if (unknown.Count > 0)
            {
                throw Rejected(e, $"Profile {Profile.Name} has no service {string.Join(", ", unknown)}. " +
                                  $"It has {string.Join(", ", _knownServices.OrderBy(s => s, StringComparer.Ordinal))}.");
            }
            return services.Distinct(StringComparer.Ordinal).ToList();
        }

        private static void CheckHosts(ModelEvent e, IReadOnlyList<string> hosts, bool allowEmpty = false)
        {
            if (!allowEmpty && hosts.Count == 0) throw Rejected(e, "The event lists no host.");
            if (hosts.Distinct(StringComparer.Ordinal).Count() != hosts.Count) throw Rejected(e, "The event lists a host twice.");
        }

        private static List<int> Pending(ModelMap map)
        {
            var pending = new List<int>();
            if (map.ForwardChains is not { } forward) return pending;
            for (var vbucket = 0; vbucket < forward.Length; vbucket++)
            {
                if (!SameChain(map.Chains[vbucket], forward[vbucket])) pending.Add(vbucket);
            }
            return pending;
        }

        private static int? FirstHolding(short[][] chains, int index)
        {
            for (var vbucket = 0; vbucket < chains.Length; vbucket++)
            {
                if (Array.IndexOf(chains[vbucket], (short)index) >= 0) return vbucket;
            }
            return null;
        }

        // A row that keeps its indexes stays the same object, so versions keep sharing it.
        private static short[] Reindexed(short[] chain, short[] newIndex)
        {
            if (chain.All(c => c < 0 || newIndex[c] == c)) return chain;
            return chain.Select(c => c < 0 ? c : newIndex[c]).ToArray();
        }

        private static bool SameChain(short[] chain, short[] other) => ReferenceEquals(chain, other) || chain.AsSpan().SequenceEqual(other);

        private Random PlanRandom(string bucket, ulong rev)
        {
            // FNV-1a, because string hash codes change from one process to the next.
            var hash = 2166136261u;
            foreach (var c in bucket)
            {
                hash = unchecked((hash ^ c) * 16777619u);
            }
            return new Random(unchecked((int)hash ^ (Seed * 486187739) ^ (int)rev * 7919));
        }

        private static string Hostname(int member) => $"node-{member}.example.com";

        private static InvalidOperationException Rejected(ModelEvent e, string reason) => new($"The model rejects {e}. {reason}");
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
