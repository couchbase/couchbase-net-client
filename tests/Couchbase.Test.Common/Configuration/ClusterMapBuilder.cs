#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.Test.Common.Configuration
{
    /// <summary>
    /// Builds cluster map JSON shaped like real server output, and parses it with the SDK deserializer.
    /// </summary>
    /// <remarks>
    /// A builder made from a <see cref="ClusterProfile"/> writes every field of the captured maps in the server order.
    /// Its node N is member N of the capture, so it gets the node UUID, server group and alternate address ports of that
    /// member. This Private Link bucket is in the middle of a rebalance from 3 to 5 nodes.
    /// <code>
    /// var profile = ClusterProfile.Load(ClusterProfile.CapellaPrivateLink);
    /// var map = ClusterMapBuilder.Bucket("recorder", profile)
    ///     .Nodes(3)
    ///     .Nodes(2, n => n.Services("kv"))
    ///     .Node("node-0.example.com", n => n.ThisNode())
    ///     .VBucketLayout(profile.VBucketLayout(3))
    ///     .ForwardMap();
    /// </code>
    /// The two new nodes have the data service only, are in the bucket and hold no vbuckets yet. The forward map spreads
    /// the vbuckets over all 5 nodes.
    /// </remarks>
    internal sealed class ClusterMapBuilder
    {
        // What a 7.x server reports for a couchbase bucket.
        private static readonly string[] DefaultBucketCapabilities =
        [
            "collections", "durableWrite", "tombstonedUserXAttrs", "couchapi", "subdoc.ReplaceBodyWithXattr",
            "subdoc.DocumentMacroSupport", "dcp", "cbhello", "touch", "cccp", "xdcrCheckpointing", "nodesExt", "xattr"
        ];

        private static readonly string[] VBucketServerMapKeyOrder =
            ["hashAlgorithm", "numReplicas", "serverList", "vBucketMap", "vBucketMapForward"];

        private static readonly Regex FakeNodeHost = new(@"^node-(\d+)\.example\.com$", RegexOptions.CultureInvariant);

        private readonly bool _isGlobal;
        private readonly string _bucketUuid;
        private readonly List<NodeBuilder> _nodes = new();
        private readonly List<string> _bucketCapabilities = new();
        private readonly SortedDictionary<string, List<string>> _clusterCapabilities = new(StringComparer.Ordinal);
        private ulong _rev = 1;
        private ulong _epoch = 1;
        private int _vbucketCount = 1024;
        private int _replicas = 1;
        private string? _clusterName;
        private string? _clusterUuid;
        private ProfileVBucketLayout? _layout;
        private bool _forwardMap;
        private ProfileVBucketLayout? _forwardLayout;

        private ClusterMapBuilder(string name, bool isGlobal, ClusterProfile? profile = null)
        {
            Name = name;
            _isGlobal = isGlobal;
            Profile = profile;
            if (isGlobal) _bucketUuid = string.Empty;
            else if (profile is not null && name == profile.BucketName && ProfileJson.Text(profile.BucketTemplate.Source["uuid"]) is { } uuid) _bucketUuid = uuid;
            else _bucketUuid = DeterministicUuid(name);
        }

        public string Name { get; }

        /// <summary>
        /// The profile the map follows, or null for a map with the fields the SDK reads.
        /// </summary>
        public ClusterProfile? Profile { get; }

        /// <summary>
        /// A global (GCCCP) map. It has no name field, so the SDK names it <see cref="BucketConfig.GlobalBucketName"/>.
        /// </summary>
        public static ClusterMapBuilder Global() => new(BucketConfig.GlobalBucketName, isGlobal: true);

        /// <summary>
        /// A global map with the constant fields of a profile, for example its capabilities and cluster labels.
        /// </summary>
        public static ClusterMapBuilder Global(ClusterProfile profile) =>
            new ClusterMapBuilder(BucketConfig.GlobalBucketName, isGlobal: true, profile).FromTemplate(profile.ClusterTemplate);

        /// <summary>
        /// A couchbase bucket map with 1024 vbuckets and one replica until <see cref="VBuckets"/> changes it.
        /// </summary>
        public static ClusterMapBuilder Bucket(string name)
        {
            var builder = new ClusterMapBuilder(name, isGlobal: false);
            builder._bucketCapabilities.AddRange(DefaultBucketCapabilities);
            return builder;
        }

        /// <summary>
        /// A bucket map with the constant fields of a profile, for example its capabilities, vbucket count and replicas.
        /// The bucket of the capture keeps its uuid. Another name gets its own uuid.
        /// </summary>
        public static ClusterMapBuilder Bucket(string name, ClusterProfile profile) =>
            new ClusterMapBuilder(name, isGlobal: false, profile).FromTemplate(profile.BucketTemplate);

        public ClusterMapBuilder Rev(ulong rev)
        {
            _rev = rev;
            return this;
        }

        public ClusterMapBuilder Epoch(ulong epoch)
        {
            _epoch = epoch;
            return this;
        }

        /// <summary>
        /// Adds a node, or changes it when the hostname is already in the map.
        /// A new node with no service gets the data service only. In a profile map, node-N.example.com is member N
        /// of the capture and another name takes the lowest free member.
        /// </summary>
        public ClusterMapBuilder Node(string hostname, Action<NodeBuilder>? configure = null)
        {
            var existing = _nodes.Find(n => n.Hostname == hostname);
            if (existing is not null)
            {
                configure?.Invoke(existing);
                return this;
            }

            AddNode(Profile is null ? new NodeBuilder(hostname) : new NodeBuilder(hostname, Profile, MemberFor(hostname)), configure);
            return this;
        }

        /// <summary>
        /// Adds nodes named node-N.example.com with uuid node-uuid-N, using the lowest free N values.
        /// In a profile map, node N is member N of the capture, so the nodes follow the join order of the capture.
        /// </summary>
        public ClusterMapBuilder Nodes(int count, Action<NodeBuilder>? configure = null)
        {
            for (var i = 0; count > 0; i++)
            {
                var hostname = $"node-{i}.example.com";
                if (_nodes.Exists(n => n.Hostname == hostname || n.Member == i)) continue;

                AddNode(Profile is null ? new NodeBuilder(hostname).Uuid($"node-uuid-{i}") : new NodeBuilder(hostname, Profile, i), configure);
                count--;
            }
            return this;
        }

        public ClusterMapBuilder RemoveNode(string hostname)
        {
            if (_nodes.RemoveAll(n => n.Hostname == hostname) == 0)
            {
                throw new ArgumentException($"The map has no node {hostname}.", nameof(hostname));
            }
            return this;
        }

        /// <summary>
        /// Adds capabilities to a section, for example ("n1ql", "enhancedPreparedStatements").
        /// </summary>
        public ClusterMapBuilder ClusterCapabilities(string section, params string[] capabilities)
        {
            if (!_clusterCapabilities.TryGetValue(section, out var existing))
            {
                existing = new List<string>();
                _clusterCapabilities[section] = existing;
            }
            AddMissing(existing, capabilities);
            return this;
        }

        /// <summary>
        /// Adds capabilities to the default set of a 7.x bucket, or to the set of the profile.
        /// </summary>
        public ClusterMapBuilder BucketCapabilities(params string[] capabilities)
        {
            ThrowIfGlobal(nameof(BucketCapabilities));
            AddMissing(_bucketCapabilities, capabilities);
            return this;
        }

        /// <summary>
        /// Sets the vbucket map size. Each write builds the map from the current bucket nodes. Without a profile
        /// or a layout it puts the vbuckets in round robin.
        /// </summary>
        public ClusterMapBuilder VBuckets(int count = 1024, int replicas = 1)
        {
            ThrowIfGlobal(nameof(VBuckets));
            _vbucketCount = count;
            _replicas = replicas;
            return this;
        }

        /// <summary>
        /// Spreads the vbuckets with the active and replica counts of a layout, for example profile.VBucketLayout(3).
        /// The largest counts go to the first bucket nodes. Bucket nodes past the layout hold no vbuckets, like nodes
        /// that joined the bucket and wait for a rebalance. Without a layout a profile map gives every bucket node the
        /// same share, in blocks of vbuckets that follow each other.
        /// </summary>
        public ClusterMapBuilder VBucketLayout(ProfileVBucketLayout? layout)
        {
            ThrowIfGlobal(nameof(VBucketLayout));
            _layout = layout;
            return this;
        }

        /// <summary>
        /// Adds vBucketMapForward, the map at the end of a rebalance. Without a layout every bucket node gets the
        /// same share.
        /// </summary>
        public ClusterMapBuilder ForwardMap(ProfileVBucketLayout? layout = null)
        {
            ThrowIfGlobal(nameof(ForwardMap));
            _forwardMap = true;
            _forwardLayout = layout;
            return this;
        }

        public ClusterMapBuilder ClearForwardMap()
        {
            _forwardMap = false;
            _forwardLayout = null;
            return this;
        }

        public ClusterMapBuilder ClusterLabels(string clusterName, string clusterUuid)
        {
            _clusterName = clusterName;
            _clusterUuid = clusterUuid;
            return this;
        }

        public ClusterMapBuilder Clone()
        {
            var clone = new ClusterMapBuilder(Name, _isGlobal, Profile)
            {
                _rev = _rev,
                _epoch = _epoch,
                _vbucketCount = _vbucketCount,
                _replicas = _replicas,
                _clusterName = _clusterName,
                _clusterUuid = _clusterUuid,
                _layout = _layout,
                _forwardMap = _forwardMap,
                _forwardLayout = _forwardLayout
            };
            clone._nodes.AddRange(_nodes.Select(n => n.Clone()));
            clone._bucketCapabilities.AddRange(_bucketCapabilities);
            foreach (var entry in _clusterCapabilities)
            {
                clone._clusterCapabilities[entry.Key] = new List<string>(entry.Value);
            }
            return clone;
        }

        public string ToJson() => Encoding.UTF8.GetString(ToUtf8Bytes());

        public byte[] ToUtf8Bytes()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                if (Profile is null) Write(writer);
                else WriteFromProfile(writer, Profile);
            }
            return stream.ToArray();
        }

        /// <summary>
        /// Parses the JSON the way the SDK does, so <see cref="BucketConfig.ConfigVersion"/> is set.
        /// </summary>
        public BucketConfig Build() =>
            JsonSerializer.Deserialize(ToUtf8Bytes(), InternalSerializationContext.Default.BucketConfig)!;

        private ClusterMapBuilder FromTemplate(ProfileTemplate template)
        {
            var body = template.Source;
            if (body["revEpoch"] is JsonValue epoch && epoch.TryGetValue<ulong>(out var value)) _epoch = value;
            _clusterUuid = ProfileJson.Text(body["clusterUUID"]);
            _clusterName = ProfileJson.Text(body["clusterName"]);
            foreach (var section in body["clusterCapabilities"] as JsonObject ?? new JsonObject())
            {
                _clusterCapabilities[section.Key] = ProfileJson.Strings(section.Value).ToList();
            }
            _bucketCapabilities.AddRange(ProfileJson.Strings(body["bucketCapabilities"]));
            _vbucketCount = ProfileJson.Number(body["numVBuckets"]) ?? _vbucketCount;
            _replicas = ProfileJson.Number(body["vBucketServerMap"]?["numReplicas"]) ?? _replicas;
            return this;
        }

        private void AddNode(NodeBuilder node, Action<NodeBuilder>? configure)
        {
            configure?.Invoke(node);
            if (!node.HasServices) node.Kv();
            _nodes.Add(node);
        }

        private int MemberFor(string hostname)
        {
            var match = FakeNodeHost.Match(hostname);
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var member)
                              && !_nodes.Exists(n => n.Member == member))
            {
                return member;
            }

            member = 0;
            while (_nodes.Exists(n => n.Member == member)) member++;
            return member;
        }

        private void Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteNumber("rev", _rev);
            writer.WriteNumber("revEpoch", _epoch);

            if (!_isGlobal)
            {
                writer.WriteString("name", Name);
                writer.WriteString("nodeLocator", "vbucket");
                writer.WriteString("uuid", _bucketUuid);
                writer.WriteString("uri", $"/pools/default/buckets/{Name}?bucket_uuid={_bucketUuid}");
                writer.WriteString("streamingUri", $"/pools/default/bucketsStreaming/{Name}?bucket_uuid={_bucketUuid}");
                writer.WriteString("bucketCapabilitiesVer", string.Empty);
                WriteStrings(writer, "bucketCapabilities", _bucketCapabilities);
                writer.WriteStartObject("ddocs");
                writer.WriteString("uri", $"/pools/default/buckets/{Name}/ddocs");
                writer.WriteEndObject();
                WriteVBucketServerMap(writer, null);
                WriteLegacyNodes(writer);
            }

            WriteNodesExt(writer);

            writer.WriteStartArray("clusterCapabilitiesVer");
            writer.WriteNumberValue(1);
            writer.WriteNumberValue(0);
            writer.WriteEndArray();
            writer.WriteStartObject("clusterCapabilities");
            foreach (var entry in _clusterCapabilities)
            {
                WriteStrings(writer, entry.Key, entry.Value);
            }
            writer.WriteEndObject();

            if (_clusterUuid is not null) writer.WriteString("clusterUUID", _clusterUuid);
            if (_clusterName is not null) writer.WriteString("clusterName", _clusterName);
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes the top level keys in the order of the profile. Keys the builder does not own come from the template.
        /// </summary>
        private void WriteFromProfile(Utf8JsonWriter writer, ClusterProfile profile)
        {
            var template = _isGlobal ? profile.ClusterTemplate : profile.BucketTemplate;
            writer.WriteStartObject();
            foreach (var key in template.KeyOrder)
            {
                switch (key)
                {
                    case "rev":
                        writer.WriteNumber(key, _rev);
                        break;
                    case "revEpoch":
                        writer.WriteNumber(key, _epoch);
                        break;
                    case "nodesExt":
                        WriteNodesExt(writer);
                        break;
                    case "clusterCapabilities":
                        writer.WriteStartObject(key);
                        var sections = (template.Source[key] as JsonObject)?.Select(s => s.Key).ToList();
                        foreach (var section in ProfileJson.InOrder(sections, _clusterCapabilities.Keys))
                        {
                            if (_clusterCapabilities.TryGetValue(section, out var capabilities)) WriteStrings(writer, section, capabilities);
                        }
                        writer.WriteEndObject();
                        break;
                    case "clusterUUID":
                        if (_clusterUuid is not null) writer.WriteString(key, _clusterUuid);
                        break;
                    case "clusterName":
                        if (_clusterName is not null) writer.WriteString(key, _clusterName);
                        break;
                    case "name" when !_isGlobal:
                        writer.WriteString(key, Name);
                        break;
                    case "uuid" when !_isGlobal:
                        writer.WriteString(key, _bucketUuid);
                        break;
                    case "numVBuckets" when !_isGlobal:
                        writer.WriteNumber(key, _vbucketCount);
                        break;
                    case "bucketCapabilities" when !_isGlobal:
                        WriteStrings(writer, key, _bucketCapabilities);
                        break;
                    case "vBucketServerMap" when !_isGlobal:
                        WriteVBucketServerMap(writer, template);
                        break;
                    case "nodes" when !_isGlobal:
                        WriteLegacyNodes(writer);
                        break;
                    default:
                        if (template.Source[key] is { } value) WriteTemplateValue(writer, template, key, key, value);
                        break;
                }
            }
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes a constant field. A string with a pattern, such as uri, gets the name and uuid of this bucket.
        /// </summary>
        private void WriteTemplateValue(Utf8JsonWriter writer, ProfileTemplate template, string path, string name, JsonNode value)
        {
            if (template.Pattern(path) is { } pattern)
            {
                writer.WriteString(name, pattern.Replace("{name}", Name).Replace("{uuid}", _bucketUuid));
                return;
            }

            if (value is JsonObject obj)
            {
                writer.WriteStartObject(name);
                foreach (var property in obj)
                {
                    if (property.Value is null) writer.WriteNull(property.Key);
                    else WriteTemplateValue(writer, template, $"{path}.{property.Key}", property.Key, property.Value);
                }
                writer.WriteEndObject();
                return;
            }

            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }

        private void WriteNodesExt(Utf8JsonWriter writer)
        {
            // The server lists the nodes outside the bucket after the bucket nodes.
            var nodes = _isGlobal ? _nodes : _nodes.Where(n => !n.IsOutsideBucket).Concat(_nodes.Where(n => n.IsOutsideBucket));
            writer.WriteStartArray("nodesExt");
            foreach (var node in nodes)
            {
                node.WriteNodesExt(writer);
            }
            writer.WriteEndArray();
        }

        private void WriteLegacyNodes(Utf8JsonWriter writer)
        {
            writer.WriteStartArray("nodes");
            foreach (var node in _nodes.Where(n => n.IsInBucket))
            {
                node.WriteLegacyNode(writer, Name, _bucketUuid);
            }
            writer.WriteEndArray();
        }

        private void WriteVBucketServerMap(Utf8JsonWriter writer, ProfileTemplate? template)
        {
            var bucketNodes = _nodes.Where(n => n.IsInBucket).ToList();
            var constant = template?.Source["vBucketServerMap"] as JsonObject;
            var order = template is { VBucketServerMapKeyOrder.Count: > 0 } ? template.VBucketServerMapKeyOrder : null;

            writer.WriteStartObject("vBucketServerMap");
            foreach (var key in ProfileJson.InOrder(order, VBucketServerMapKeyOrder))
            {
                switch (key)
                {
                    case "hashAlgorithm":
                        writer.WriteString(key, ProfileJson.Text(constant?[key]) ?? "CRC");
                        break;
                    case "numReplicas":
                        writer.WriteNumber(key, _replicas);
                        break;
                    case "serverList":
                        writer.WriteStartArray(key);
                        foreach (var node in bucketNodes)
                        {
                            writer.WriteStringValue(node.HostPort(node.Port("kv")));
                        }
                        writer.WriteEndArray();
                        break;
                    case "vBucketMap":
                        WriteChains(writer, key, VBucketChains(bucketNodes.Count, _layout, roundRobin: Profile is null && _layout is null));
                        break;
                    case "vBucketMapForward":
                        if (_forwardMap) WriteChains(writer, key, VBucketChains(bucketNodes.Count, _forwardLayout, roundRobin: false));
                        break;
                    default:
                        if (constant?[key] is { } value)
                        {
                            writer.WritePropertyName(key);
                            value.WriteTo(writer);
                        }
                        break;
                }
            }
            writer.WriteEndObject();
        }

        /// <summary>
        /// The copies of each vbucket, active first, as indexes into serverList. A missing copy is -1.
        /// </summary>
        private short[][] VBucketChains(int servers, ProfileVBucketLayout? layout, bool roundRobin)
        {
            var copies = _replicas + 1;
            var chains = new short[_vbucketCount][];
            if (roundRobin)
            {
                // The active copy is on node vbucket % n and each replica on the next node.
                for (var vbucket = 0; vbucket < _vbucketCount; vbucket++)
                {
                    chains[vbucket] = new short[copies];
                    for (var copy = 0; copy < copies; copy++)
                    {
                        chains[vbucket][copy] = (short)(copy < servers ? (vbucket + copy) % servers : -1);
                    }
                }
                return chains;
            }

            if (layout is not null) CheckLayout(layout, servers);
            var active = Counts(layout?.ActivePerNode, servers, _vbucketCount);
            var replicas = Counts(layout?.ReplicaPerNode, servers, _vbucketCount * _replicas);

            // The active copies fill blocks in node order. Each replica goes to the node that has the least room left to
            // reach its replica count, so the counts of a layout are met whenever they can be. Ties go round robin.
            var notActive = new int[servers];
            for (var node = 0; node < servers; node++) notActive[node] = _vbucketCount - active[node];
            var owner = 0;
            var ownerLeft = servers > 0 ? active[0] : 0;
            for (var vbucket = 0; vbucket < _vbucketCount; vbucket++)
            {
                while (ownerLeft == 0 && owner < servers - 1) ownerLeft = active[++owner];
                var chain = Enumerable.Repeat((short)-1, copies).ToArray();
                chains[vbucket] = chain;
                if (ownerLeft == 0) continue;

                chain[0] = (short)owner;
                ownerLeft--;
                for (var copy = 1; copy < copies; copy++)
                {
                    var best = -1;
                    for (var step = 0; step < servers; step++)
                    {
                        var node = (owner + 1 + vbucket + step) % servers;
                        if (Array.IndexOf(chain, (short)node) < 0 && (best < 0 || Before(node, best))) best = node;
                    }
                    if (best < 0) break;
                    chain[copy] = (short)best;
                    replicas[best]--;
                }

                for (var node = 0; node < servers; node++)
                {
                    if (node != owner) notActive[node]--;
                }
            }
            return chains;

            bool Before(int node, int other) =>
                replicas[node] > 0 != replicas[other] > 0
                    ? replicas[node] > 0
                    : notActive[node] - replicas[node] < notActive[other] - replicas[other];
        }

        private void CheckLayout(ProfileVBucketLayout layout, int servers)
        {
            if (layout.VBuckets != _vbucketCount || layout.Copies != _replicas + 1 || layout.ActivePerNode.Sum() != _vbucketCount)
            {
                throw new InvalidOperationException(
                    $"The layout spreads {layout.ActivePerNode.Sum()} of {layout.VBuckets} vbuckets with {layout.Copies} copies, " +
                    $"but the map has {_vbucketCount} vbuckets with {_replicas + 1} copies.");
            }
            if (layout.ActivePerNode.Count > servers)
            {
                throw new InvalidOperationException($"The layout is for {layout.ActivePerNode.Count} nodes, but the bucket has {servers}.");
            }
        }

        // Layout counts go to the nodes from the largest down, so the last nodes get the smallest counts.
        // Without a layout every node gets the same share.
        private static int[] Counts(IReadOnlyList<int>? perNode, int servers, int total)
        {
            var counts = new int[servers];
            if (perNode is null)
            {
                for (var node = 0; node < servers; node++) counts[node] = total / servers + (node < total % servers ? 1 : 0);
                return counts;
            }

            var sorted = perNode.OrderByDescending(c => c).ToList();
            for (var node = 0; node < sorted.Count && node < servers; node++) counts[node] = sorted[node];
            return counts;
        }

        private static void WriteChains(Utf8JsonWriter writer, string propertyName, short[][] chains)
        {
            writer.WriteStartArray(propertyName);
            foreach (var chain in chains)
            {
                writer.WriteStartArray();
                foreach (var copy in chain)
                {
                    writer.WriteNumberValue(copy);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }

        private void ThrowIfGlobal(string method)
        {
            if (_isGlobal) throw new InvalidOperationException($"A global map has no buckets, so {method} does not apply.");
        }

        private static void WriteStrings(Utf8JsonWriter writer, string propertyName, IEnumerable<string> values)
        {
            writer.WriteStartArray(propertyName);
            foreach (var value in values)
            {
                writer.WriteStringValue(value);
            }
            writer.WriteEndArray();
        }

        private static void AddMissing(List<string> target, IEnumerable<string> values)
        {
            foreach (var value in values)
            {
                if (!target.Contains(value)) target.Add(value);
            }
        }

        // FNV-1a, so a bucket name always gets the same 32 hex character uuid.
        private static string DeterministicUuid(string seed)
        {
            var hash = 14695981039346656037UL;
            foreach (var c in seed)
            {
                hash = (hash ^ c) * 1099511628211UL;
            }
            return $"{hash:x16}{~hash:x16}";
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
