#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.Test.Common.Configuration.Model
{
    /// <summary>
    /// One map of a <see cref="ClusterModel"/> at one version. A version shares the chain rows that did not change
    /// with the version before it, so the rows must not change.
    /// </summary>
    internal sealed class ModelMap
    {
        private readonly short[][] _chains;
        private readonly short[][]? _forward;

        private ModelMap(string? bucket, ConfigVersion version, IReadOnlyList<ModelNode> nodes, IReadOnlyList<string> bucketNodes,
            short[][] chains, short[][]? forward, int replicas)
        {
            Bucket = bucket;
            Version = version;
            Nodes = nodes;
            BucketNodes = bucketNodes;
            _chains = chains;
            _forward = forward;
            Replicas = replicas;
        }

        /// <summary>
        /// The bucket name, or null for the cluster map.
        /// </summary>
        public string? Bucket { get; }

        public ConfigVersion Version { get; }

        /// <summary>
        /// The nodes in nodesExt order. The cluster map lists the nodes in ordinal hostname order. A bucket map lists the
        /// bucket nodes first in serverList order, then the other nodes in the order of the cluster map.
        /// </summary>
        public IReadOnlyList<ModelNode> Nodes { get; }

        /// <summary>
        /// The hostnames in serverList order. The cluster map has none.
        /// </summary>
        public IReadOnlyList<string> BucketNodes { get; }

        public int Replicas { get; }

        public int VBucketCount => _chains.Length;

        /// <summary>
        /// The chain of each vbucket, active first, as indexes into <see cref="BucketNodes"/> and -1 for a missing copy.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<short>> VBucketMap => _chains;

        /// <summary>
        /// The forward chains in the form of <see cref="VBucketMap"/>, or null when no rebalance is running.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<short>>? ForwardMap => _forward;

        internal short[][] Chains => _chains;

        internal short[][]? ForwardChains => _forward;

        internal static ModelMap ForCluster(ConfigVersion version, IReadOnlyList<ModelNode> nodes) =>
            new(null, version, nodes, [], [], null, 0);

        /// <param name="clusterNodes">Every node of the cluster, in the order of the cluster map.</param>
        internal static ModelMap ForBucket(string bucket, ConfigVersion version, IReadOnlyList<ModelNode> clusterNodes,
            IReadOnlyList<string> bucketNodes, short[][] chains, short[][]? forward, int replicas)
        {
            var inBucket = new HashSet<string>(bucketNodes, StringComparer.Ordinal);
            var nodes = bucketNodes.Select(host => clusterNodes.First(n => n.Hostname == host))
                .Concat(clusterNodes.Where(n => !inBucket.Contains(n.Hostname)))
                .ToList();
            return new ModelMap(bucket, version, nodes, bucketNodes, chains, forward, replicas);
        }

        public ModelNode? Node(string hostname) => Nodes.FirstOrDefault(n => n.Hostname == hostname);

        public bool IsBucketNode(string hostname) => BucketNodes.Contains(hostname);

        /// <summary>
        /// The hostnames of the copies of a vbucket, active first, and null for a missing copy.
        /// </summary>
        public IReadOnlyList<string?> Chain(int vbucket) => Hosts(_chains[vbucket]);

        /// <summary>
        /// The forward chain in the form of <see cref="Chain"/>, or null when no rebalance is running.
        /// </summary>
        public IReadOnlyList<string?>? ForwardChain(int vbucket) => _forward is null ? null : Hosts(_forward[vbucket]);

        public string? ActiveHost(int vbucket) => Host(_chains[vbucket][0]);

        public override string ToString() => $"{Bucket ?? "cluster"} {Version}";

        private IReadOnlyList<string?> Hosts(short[] chain) => chain.Select(Host).ToList();

        private string? Host(short index) => index < 0 ? null : BucketNodes[index];
    }

    /// <summary>
    /// One node of a <see cref="ClusterModel"/>. A member is one node with one node UUID, so a host that rejoins is a
    /// new member. A recovered host keeps its member.
    /// </summary>
    internal sealed class ModelNode(string hostname, string nodeUuid, int member, string? serverGroup, IReadOnlyList<string> services)
    {
        public string Hostname { get; } = hostname;

        public string NodeUuid { get; } = nodeUuid;

        public int Member { get; } = member;

        public string? ServerGroup { get; } = serverGroup;

        /// <summary>
        /// Service names that <see cref="NodeBuilder.Services"/> takes, for example kv, n1ql and index.
        /// </summary>
        public IReadOnlyList<string> Services { get; } = services;

        public bool Has(string service) => Services.Contains(service);

        internal ModelNode WithServices(IReadOnlyList<string> services) => new(Hostname, NodeUuid, Member, ServerGroup, services);

        public override string ToString() => $"{Hostname} member {Member} {ServerGroup} [{string.Join(", ", Services)}]";
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
