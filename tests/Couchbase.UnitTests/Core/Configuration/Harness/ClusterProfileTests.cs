#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Test.Common.Configuration;
using Xunit;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    public class ClusterProfileTests
    {
        // The server sends these, but the SDK config classes have no property for them.
        private static readonly string[] SdkIgnoredPaths =
        [
            "prodName", "bucketType", "storageBackend", "numVBuckets", "collectionsManifestUid",
            "nodes[].alternateAddresses.external.hostname"
        ];

        private static readonly string[] Kinds = [ClusterProfile.ClusterKind, ClusterProfile.BucketKind];

        private static readonly Regex Index = new(@"\[\d+\]", RegexOptions.CultureInvariant);

        public static TheoryData<string> Profiles()
        {
            var data = new TheoryData<string>();
            foreach (var name in ClusterProfile.Names) data.Add(name);
            return data;
        }

        public static TheoryData<string, string> Examples()
        {
            var data = new TheoryData<string, string>();
            foreach (var name in ClusterProfile.Names)
            {
                foreach (var example in ClusterProfile.Load(name).Examples) data.Add(name, example.FileName);
            }
            return data;
        }

        public static TheoryData<string, int> Layouts()
        {
            var data = new TheoryData<string, int>();
            foreach (var name in ClusterProfile.Names)
            {
                foreach (var layout in ClusterProfile.Load(name).VBucketLayouts) data.Add(name, layout.Nodes);
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(Profiles))]
        public void Load_GivesTypedAccessToTheProfile(string profile)
        {
            var loaded = ClusterProfile.Load(profile);

            Assert.Same(loaded, ClusterProfile.Load(profile));
            Assert.Equal(profile, loaded.Name);
            Assert.Equal(["rev", "nodesExt"], loaded.ClusterTemplate.KeyOrder.Take(2));
            Assert.Contains("vBucketServerMap", loaded.BucketTemplate.KeyOrder);
            Assert.Equal(loaded.BucketName, loaded.BucketTemplate.Body["name"]!.GetValue<string>());
            Assert.Equal(11210, loaded.NodeShape.Ports["kv"]);
            Assert.NotEmpty(loaded.NodeShape.ServiceSets);
            Assert.NotEmpty(loaded.NodeShape.Members);
            Assert.NotEmpty(loaded.VBucketLayouts);
            Assert.NotEmpty(loaded.Fields(ClusterProfile.BucketKind));
            Assert.NotEmpty(loaded.Examples);
            Assert.All(loaded.Examples, example =>
            {
                using var doc = JsonDocument.Parse(example.Json);
                Assert.Equal(example.NodesExt, doc.RootElement.GetProperty("nodesExt").GetArrayLength());
                Assert.Equal(example.Kind == ClusterProfile.BucketKind, doc.RootElement.TryGetProperty("name", out _));
            });
        }

        [Fact]
        public void Load_UnknownProfile_ListsTheProfiles()
        {
            Assert.Equal([ClusterProfile.CapellaPrivateLink, ClusterProfile.CapellaPublic, ClusterProfile.Docker], ClusterProfile.Names);

            var error = Assert.Throws<ArgumentException>(() => ClusterProfile.Load("missing"));
            Assert.Contains(ClusterProfile.CapellaPublic, error.Message);
        }

        /// <summary>
        /// The built map has the same leaves as the example, in the same order. Only the vbucket map entries differ.
        /// </summary>
        [Theory]
        [MemberData(nameof(Examples))]
        public void Example_BuiltFromProfile_HasSameFields(string profile, string example)
        {
            var loaded = ClusterProfile.Load(profile);
            var expected = loaded.Example(example);

            var built = BuildLike(loaded, expected);

            Assert.Equal(Leaves(expected.Json), Leaves(built.ToJson()));
        }

        [Fact]
        public void PrivateLinkRebalanceIn_BuiltByHand_MatchesExample()
        {
            var profile = ClusterProfile.Load(ClusterProfile.CapellaPrivateLink);
            var example = profile.Example("bucket-5nodes-forward-map.json");

            var built = ClusterMapBuilder.Bucket("recorder", profile).Rev(example.Rev)
                .Nodes(3)
                .Nodes(2, n => n.Services("kv"))
                .Node("node-3.example.com", n => n.ThisNode())
                .VBucketLayout(profile.VBucketLayout(3))
                .ForwardMap();

            Assert.Equal(Leaves(example.Json), Leaves(built.ToJson()));
            var map = built.Build().VBucketServerMap;
            Assert.Equal(new short[] { 0, 1, 2 }, map.VBucketMap.Select(c => c[0]).Distinct().OrderBy(i => i));
            Assert.Equal(new short[] { 0, 1, 2, 3, 4 }, map.VBucketMapForward.Select(c => c[0]).Distinct().OrderBy(i => i));
        }

        [Fact]
        public void PrivateLinkNodeLeavingBucket_BuiltByHand_MatchesExample()
        {
            var profile = ClusterProfile.Load(ClusterProfile.CapellaPrivateLink);
            var example = profile.Example("bucket-5nodes-node-outside-bucket.json");

            var built = ClusterMapBuilder.Bucket("recorder", profile).Rev(example.Rev)
                .Nodes(5)
                .Node("node-1.example.com", n => n.ThisNode())
                .Node("node-3.example.com", n => n.OutsideBucket());

            Assert.Equal(Leaves(example.Json), Leaves(built.ToJson()));
        }

        [Theory]
        [MemberData(nameof(Examples))]
        public void Example_BuiltFromProfile_GivesSameSdkView(string profile, string example)
        {
            var loaded = ClusterProfile.Load(profile);
            var expected = loaded.Example(example);

            var built = BuildLike(loaded, expected).ToUtf8Bytes();

            foreach (var network in new[] { NetworkResolution.Default, NetworkResolution.External })
            {
                Assert.Equal(SdkView(Encoding.UTF8.GetBytes(expected.Json), network), SdkView(built, network));
            }
        }

        /// <summary>
        /// The builder can write every field of the captures, and writes no other field.
        /// </summary>
        [Theory]
        [MemberData(nameof(Profiles))]
        public void Builder_WritesEveryProfileFieldAndNoOther(string profile)
        {
            var loaded = ClusterProfile.Load(profile);
            foreach (var kind in Kinds)
            {
                var fields = loaded.Fields(kind).ToDictionary(f => f.Path, f => f.Types);
                var written = Leaves(EveryShape(loaded, kind).ToJson(), withValues: false)
                    .Select(leaf => leaf.Split(' '))
                    .GroupBy(leaf => Index.Replace(leaf[0], "[]"), leaf => leaf[1])
                    .ToDictionary(g => g.Key, g => g.Distinct().ToList());

                Assert.Empty(fields.Keys.Where(path => !written.ContainsKey(path)).Select(path => $"{kind} {path} is not written"));
                Assert.Empty(written.Where(w => !fields.ContainsKey(w.Key) || w.Value.Except(fields[w.Key]).Any())
                    .Select(w => $"{kind} {w.Key} {string.Join(",", w.Value)} is not in the profile"));
            }
        }

        /// <summary>
        /// A new field in a future capture makes this test fail until the SDK models it or the list names it.
        /// </summary>
        [Theory]
        [MemberData(nameof(Profiles))]
        public void Sdk_ModelsEveryProfileFieldOrIgnoresIt(string profile)
        {
            var loaded = ClusterProfile.Load(profile);

            var unknown = Kinds.SelectMany(loaded.Fields)
                .Where(f => !f.ModeledBySdk && !SdkIgnoredPaths.Contains(f.Path))
                .Select(f => f.ToString());

            Assert.Empty(unknown);
        }

        [Fact]
        public void SdkIgnoredPaths_AreStillSentBySomeProfile()
        {
            var notModeled = ClusterProfile.Names.Select(ClusterProfile.Load)
                .SelectMany(p => Kinds.SelectMany(p.Fields))
                .Where(f => !f.ModeledBySdk)
                .Select(f => f.Path)
                .ToHashSet();

            Assert.All(SdkIgnoredPaths, path => Assert.Contains(path, notModeled));
        }

        [Theory]
        [MemberData(nameof(Layouts))]
        public void VBucketLayout_FromProfile_HasProfileCounts(string profile, int nodes)
        {
            var loaded = ClusterProfile.Load(profile);
            var layout = loaded.VBucketLayout(nodes)!;

            var map = ClusterMapBuilder.Bucket(loaded.BucketName, loaded).Nodes(nodes).VBucketLayout(layout).Build()
                .VBucketServerMap.VBucketMap;

            Assert.Equal(layout.VBuckets, map.Length);
            Assert.All(map, chain => Assert.Equal(layout.Copies, chain.Length));
            Assert.Equal(layout.ActivePerNode, CopiesPerServer(map, nodes, chain => chain.Take(1)));
            Assert.Equal(layout.ReplicaPerNode, CopiesPerServer(map, nodes, chain => chain.Skip(1)));
            Assert.Equal(layout.ChainsWithMissingCopy, map.Count(chain => chain.Contains((short)-1)));
            Assert.Equal(layout.ChainsWithRepeatedNode, map.Count(chain => chain.Distinct().Count() < chain.Length));
        }

        [Fact]
        public void Nodes_WithPrivateLinkProfile_FollowJoinOrderPastTheCapture()
        {
            var profile = ClusterProfile.Load(ClusterProfile.CapellaPrivateLink);

            var config = ClusterMapBuilder.Bucket("recorder", profile).Nodes(7).Build();

            Assert.Equal(Enumerable.Range(0, 7).Select(i => $"node-{i}.example.com"), config.NodesExt.Select(n => n.Hostname));
            Assert.Equal(Enumerable.Range(0, 7).Select(i => "a" + i.ToString("x31")), config.NodesExt.Select(n => n.NodeUuid));
            Assert.Equal(["group:2", "group:3", "group:1", "group:1", "group:2", "group:3", "group:1"],
                config.NodesExt.Select(n => n.ServerGroup));
            Assert.All(config.NodesExt, n => Assert.Equal("private-endpoint.example.com", n.AlternateAddresses["external"].Hostname));
            Assert.Equal([11210, 11209, 11208, 11211, 11212, 11213, 11214],
                config.NodesExt.Select(n => n.AlternateAddresses["external"].Ports.KvSsl));
            Assert.All(config.NodesExt, n => Assert.Equal(8093, n.Services.N1Ql));

            // Without a layout every node has the same share of active and replica copies.
            var map = config.VBucketServerMap.VBucketMap;
            Assert.Equal([146, 146, 146, 146, 146, 147, 147], CopiesPerServer(map, 7, chain => chain.Take(1)));
            Assert.Equal([146, 146, 146, 146, 146, 147, 147], CopiesPerServer(map, 7, chain => chain.Skip(1)));
            Assert.All(map, chain => Assert.NotEqual(chain[0], chain[1]));
        }

        [Fact]
        public void Services_WithProfile_SetsProfilePortsAndAlternatePortsFollow()
        {
            var profile = ClusterProfile.Load(ClusterProfile.CapellaPrivateLink);
            var dataOnly = profile.NodeShape.ServiceSets.Single(s => s.Services.SequenceEqual(["capi", "kv", "mgmt", "projector"]));

            var builder = ClusterMapBuilder.Global(profile).Nodes(1, n => n.Services("kv"));

            using var doc = JsonDocument.Parse(builder.ToJson());
            var node = doc.RootElement.GetProperty("nodesExt")[0];
            Assert.Equal(dataOnly.Keys, node.GetProperty("services").EnumerateObject().Select(p => p.Name));
            Assert.Equal(["mgmtSSL", "kvSSL", "capiSSL"],
                node.GetProperty("alternateAddresses").GetProperty("external").GetProperty("ports").EnumerateObject().Select(p => p.Name));

            Assert.Throws<ArgumentException>(() => ClusterMapBuilder.Global(profile).Nodes(1, n => n.Services("fts")));
            Assert.Throws<InvalidOperationException>(() => ClusterMapBuilder.Global().Nodes(1, n => n.Services("kv")));
        }

        /// <summary>
        /// Builds the map of an example from the profile. The example gives only its shape. That is the nodes and
        /// their services, the node that sent it, the nodes outside the bucket, how many nodes hold vbuckets and
        /// whether it has a forward map. Everything else comes from the profile.
        /// </summary>
        private static ClusterMapBuilder BuildLike(ClusterProfile profile, ProfileExample example)
        {
            using var doc = JsonDocument.Parse(example.Json);
            var root = doc.RootElement;
            var isBucket = example.Kind == ClusterProfile.BucketKind;
            var builder = isBucket
                ? ClusterMapBuilder.Bucket(root.GetProperty("name").GetString()!, profile)
                : ClusterMapBuilder.Global(profile);
            builder.Rev(example.Rev);

            var serverList = isBucket
                ? root.GetProperty("vBucketServerMap").GetProperty("serverList").EnumerateArray()
                    .Select(s => s.GetString()!).Select(s => s.Substring(0, s.LastIndexOf(':'))).ToList()
                : new List<string>();
            foreach (var node in root.GetProperty("nodesExt").EnumerateArray())
            {
                var hostname = node.GetProperty("hostname").GetString()!;
                var services = node.GetProperty("services").EnumerateObject()
                    .Select(p => ProfileNodeShape.ServiceOf(p.Name)).Distinct().ToArray();
                var thisNode = node.TryGetProperty("thisNode", out var flag) && flag.GetBoolean();
                var outside = isBucket && !serverList.Contains(hostname);
                builder.Node(hostname, n =>
                {
                    n.Services(services);
                    if (thisNode) n.ThisNode();
                    if (outside) n.OutsideBucket();
                });
            }

            if (isBucket)
            {
                var map = root.GetProperty("vBucketServerMap");
                var holders = map.GetProperty("vBucketMap").EnumerateArray().Select(chain => chain[0].GetInt32()).Distinct().Count();
                if (holders < serverList.Count) builder.VBucketLayout(profile.VBucketLayout(holders));
                if (map.TryGetProperty("vBucketMapForward", out _)) builder.ForwardMap();
            }
            return builder;
        }

        /// <summary>
        /// One map of a kind that uses every knob, with one node per service set of the profile.
        /// </summary>
        private static ClusterMapBuilder EveryShape(ClusterProfile profile, string kind)
        {
            var builder = kind == ClusterProfile.BucketKind
                ? ClusterMapBuilder.Bucket(profile.BucketName, profile).ForwardMap()
                : ClusterMapBuilder.Global(profile);
            builder.Nodes(Math.Max(profile.NodeShape.Members.Count, profile.NodeShape.ServiceSets.Count));
            for (var i = 0; i < profile.NodeShape.ServiceSets.Count; i++)
            {
                var services = profile.NodeShape.ServiceSets[i].Services.ToArray();
                builder.Node($"node-{i}.example.com", n => n.Services(services));
            }
            return builder.Node("node-0.example.com", n => n.ThisNode());
        }

        /// <summary>
        /// Every leaf of a JSON document in document order, as "path type value". Empty objects and arrays are leaves.
        /// The vbucket map entries have no value, because the builder places vbuckets in its own way.
        /// </summary>
        private static List<string> Leaves(string json, bool withValues = true)
        {
            using var doc = JsonDocument.Parse(json);
            var leaves = new List<string>();
            AddLeaves(doc.RootElement, string.Empty, leaves, withValues);
            return leaves;
        }

        private static void AddLeaves(JsonElement element, string path, List<string> leaves, bool withValues)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var properties = 0;
                    foreach (var property in element.EnumerateObject())
                    {
                        AddLeaves(property.Value, path.Length == 0 ? property.Name : $"{path}.{property.Name}", leaves, withValues);
                        properties++;
                    }
                    if (properties == 0) leaves.Add($"{path} object {{}}");
                    break;
                case JsonValueKind.Array:
                    var items = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        AddLeaves(item, $"{path}[{items}]", leaves, withValues);
                        items++;
                    }
                    if (items == 0) leaves.Add($"{path} array []");
                    break;
                case JsonValueKind.String:
                    leaves.Add(Leaf(path, "string", $"\"{element.GetString()}\"", withValues));
                    break;
                case JsonValueKind.Number:
                    leaves.Add(Leaf(path, "number", element.GetRawText(), withValues));
                    break;
                default:
                    leaves.Add(Leaf(path, element.ValueKind == JsonValueKind.Null ? "null" : "boolean", element.GetRawText(), withValues));
                    break;
            }
        }

        private static string Leaf(string path, string type, string value, bool withValues) =>
            withValues && !path.StartsWith("vBucketServerMap.vBucketMap", StringComparison.Ordinal) ? $"{path} {type} {value}" : $"{path} {type}";

        /// <summary>
        /// How the SDK sees a map on one network. These are the nodes with their hosts, ports and services, the KV
        /// endpoints with and without TLS, and the server list of the vbucket map.
        /// </summary>
        private static List<string> SdkView(byte[] json, string network)
        {
            var config = JsonSerializer.Deserialize(json, InternalSerializationContext.Default.BucketConfig)!;
            config.NetworkResolution = network;
            var tls = new ClusterOptions { EnableTls = true };
            var plain = new ClusterOptions { EnableTls = false };

            var view = config.GetNodes()
                .Select(n => $"{network} {Describe(n)} endpoint={HostEndpointWithPort.Create(n, tls)}/{HostEndpointWithPort.Create(n, plain)}")
                .ToList();
            view.AddRange(config.VBucketServerMap.ServerList.Select(server => $"{network} server {server}"));
            return view;
        }

        private static string Describe(NodeAdapter node) =>
            $"{node.Hostname} canonical={node.CanonicalHostname} uuid={node.NodeUuid} kvNode={node.IsKvNode} " +
            $"mgmt={node.MgmtApi}/{node.MgmtApiSsl} kv={node.KeyValue}/{node.KeyValueSsl} views={node.Views}/{node.ViewsSsl} " +
            $"query={node.N1Ql}/{node.N1QlSsl} search={node.Fts}/{node.FtsSsl} analytics={node.Analytics}/{node.AnalyticsSsl} " +
            $"eventing={node.Eventing}/{node.EventingSsl} projector={node.Projector} index={node.IndexAdmin}/{node.IndexScan}/" +
            $"{node.IndexHttp}/{node.IndexStreamInit}/{node.IndexStreamCatchup}/{node.IndexStreamMaint} api={node.CouchbaseApiBase}";

        private static List<int> CopiesPerServer(short[][] map, int servers, Func<short[], IEnumerable<short>> copies) =>
            Enumerable.Range(0, servers).Select(server => map.Count(chain => copies(chain).Contains((short)server))).OrderBy(n => n).ToList();
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
