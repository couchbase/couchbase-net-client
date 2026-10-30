#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.Test.Common.Configuration
{
    /// <summary>
    /// Builds cluster map JSON shaped like real server output, and parses it with the SDK deserializer.
    /// </summary>
    internal sealed class ClusterMapBuilder
    {
        // What a 7.x server reports for a couchbase bucket.
        private static readonly string[] DefaultBucketCapabilities =
        [
            "collections", "durableWrite", "tombstonedUserXAttrs", "couchapi", "subdoc.ReplaceBodyWithXattr",
            "subdoc.DocumentMacroSupport", "dcp", "cbhello", "touch", "cccp", "xdcrCheckpointing", "nodesExt", "xattr"
        ];

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

        private ClusterMapBuilder(string name, bool isGlobal)
        {
            Name = name;
            _isGlobal = isGlobal;
            _bucketUuid = isGlobal ? string.Empty : DeterministicUuid(name);
        }

        public string Name { get; }

        /// <summary>
        /// A global (GCCCP) map. It has no name field, so the SDK names it <see cref="BucketConfig.GlobalBucketName"/>.
        /// </summary>
        public static ClusterMapBuilder Global() => new(BucketConfig.GlobalBucketName, isGlobal: true);

        /// <summary>
        /// A couchbase bucket map with 1024 vbuckets and one replica until <see cref="VBuckets"/> changes it.
        /// </summary>
        public static ClusterMapBuilder Bucket(string name)
        {
            var builder = new ClusterMapBuilder(name, isGlobal: false);
            builder._bucketCapabilities.AddRange(DefaultBucketCapabilities);
            return builder;
        }

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
        /// A new node with no service gets the data service only.
        /// </summary>
        public ClusterMapBuilder Node(string hostname, Action<NodeBuilder>? configure = null)
        {
            var existing = _nodes.Find(n => n.Hostname == hostname);
            if (existing is not null)
            {
                configure?.Invoke(existing);
                return this;
            }

            AddNode(new NodeBuilder(hostname), configure);
            return this;
        }

        /// <summary>
        /// Adds nodes named node-N.example.com with uuid node-uuid-N, using the lowest free N values.
        /// </summary>
        public ClusterMapBuilder Nodes(int count, Action<NodeBuilder>? configure = null)
        {
            for (var i = 0; count > 0; i++)
            {
                var hostname = $"node-{i}.example.com";
                if (_nodes.Exists(n => n.Hostname == hostname)) continue;

                AddNode(new NodeBuilder(hostname).Uuid($"node-uuid-{i}"), configure);
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
        /// Adds capabilities to the default set of a 7.x bucket.
        /// </summary>
        public ClusterMapBuilder BucketCapabilities(params string[] capabilities)
        {
            ThrowIfGlobal(nameof(BucketCapabilities));
            AddMissing(_bucketCapabilities, capabilities);
            return this;
        }

        /// <summary>
        /// Sets the vbucket map size. Each write builds the map from the current data nodes in round robin.
        /// </summary>
        public ClusterMapBuilder VBuckets(int count = 1024, int replicas = 1)
        {
            ThrowIfGlobal(nameof(VBuckets));
            _vbucketCount = count;
            _replicas = replicas;
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
            var clone = new ClusterMapBuilder(Name, _isGlobal)
            {
                _rev = _rev,
                _epoch = _epoch,
                _vbucketCount = _vbucketCount,
                _replicas = _replicas,
                _clusterName = _clusterName,
                _clusterUuid = _clusterUuid
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
                Write(writer);
            }
            return stream.ToArray();
        }

        /// <summary>
        /// Parses the JSON the way the SDK does, so <see cref="BucketConfig.ConfigVersion"/> is set.
        /// </summary>
        public BucketConfig Build() =>
            JsonSerializer.Deserialize(ToUtf8Bytes(), InternalSerializationContext.Default.BucketConfig)!;

        private void AddNode(NodeBuilder node, Action<NodeBuilder>? configure)
        {
            configure?.Invoke(node);
            if (!node.HasServices) node.Kv();
            _nodes.Add(node);
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
                WriteVBucketServerMap(writer);

                writer.WriteStartArray("nodes");
                foreach (var node in _nodes.Where(n => n.IsKv))
                {
                    node.WriteLegacyNode(writer, Name, _bucketUuid);
                }
                writer.WriteEndArray();
            }

            writer.WriteStartArray("nodesExt");
            foreach (var node in _nodes)
            {
                node.WriteNodesExt(writer);
            }
            writer.WriteEndArray();

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

        private void WriteVBucketServerMap(Utf8JsonWriter writer)
        {
            var kvNodes = _nodes.Where(n => n.IsKv).ToList();

            writer.WriteStartObject("vBucketServerMap");
            writer.WriteString("hashAlgorithm", "CRC");
            writer.WriteNumber("numReplicas", _replicas);
            writer.WriteStartArray("serverList");
            foreach (var node in kvNodes)
            {
                writer.WriteStringValue(node.HostPort(node.Port("kv")));
            }
            writer.WriteEndArray();

            writer.WriteStartArray("vBucketMap");
            for (var vbucket = 0; vbucket < _vbucketCount; vbucket++)
            {
                // The active copy is on node vbucket % n and each replica on the next node. Missing copies are -1.
                writer.WriteStartArray();
                for (var copy = 0; copy <= _replicas; copy++)
                {
                    writer.WriteNumberValue(copy < kvNodes.Count ? (vbucket + copy) % kvNodes.Count : -1);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
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
