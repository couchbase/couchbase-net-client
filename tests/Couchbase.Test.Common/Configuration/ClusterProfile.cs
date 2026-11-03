#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Couchbase.Test.Common.Configuration
{
    /// <summary>
    /// A cluster map profile that the reduce command of tests/Couchbase.ConfigRecorder wrote from live captures.
    /// It holds the constant part of each map kind, the shape of the nodes, the vbucket layouts and a few real maps.
    /// Profiles are embedded resources under Configuration/Profiles/{name}/.
    /// </summary>
    internal sealed class ClusterProfile
    {
        public const string CapellaPublic = "capella-public";
        public const string CapellaPrivateLink = "capella-privatelink";
        public const string Docker = "docker";

        private const string ResourcePrefix = "Couchbase.Test.Common.Configuration.Profiles.";
        private const string ProfileFile = ".profile.json";

        private static readonly ConcurrentDictionary<string, ClusterProfile> Loaded = new(StringComparer.Ordinal);
        private static readonly Lazy<IReadOnlyList<string>> AllNames = new(FindNames);

        private readonly string _resourceFolder;
        private readonly string _json;
        private readonly Dictionary<string, IReadOnlyList<ProfileField>> _fields = new(StringComparer.Ordinal);

        private ClusterProfile(string resourceFolder, string json)
        {
            _resourceFolder = resourceFolder;
            _json = json;
            var root = JsonNode.Parse(json)!.AsObject();

            Name = ProfileJson.Text(root["name"]) ?? resourceFolder;
            ServerVersion = ProfileJson.Text(root["source"]?["serverVersion"]);
            ClusterTemplate = new ProfileTemplate(ClusterKind, root["template"]![ClusterKind]!.AsObject());
            BucketTemplate = new ProfileTemplate(BucketKind, root["template"]![BucketKind]!.AsObject());
            NodeShape = new ProfileNodeShape(root["nodeShape"]!.AsObject());

            var fields = root["fields"]!.AsObject();
            foreach (var kind in new[] { ClusterKind, BucketKind })
            {
                _fields[kind] = ProfileJson.Items(fields[kind]).Select(f => new ProfileField(kind, f!.AsObject())).ToList();
            }
            SdkPathsNotSent = ProfileJson.Strings(fields["sdkPathsNotSent"]);
            Examples = ProfileJson.Items(root["examples"]).Select(e => new ProfileExample(this, e!.AsObject())).ToList();
        }

        public const string ClusterKind = "cluster";
        public const string BucketKind = "bucket";

        /// <summary>
        /// The names of the embedded profiles, for example capella-privatelink.
        /// </summary>
        public static IReadOnlyList<string> Names => AllNames.Value;

        public string Name { get; }

        public string? ServerVersion { get; }

        /// <summary>
        /// The constant fields of a global map, as the server sent them.
        /// </summary>
        public ProfileTemplate ClusterTemplate { get; }

        /// <summary>
        /// The constant fields of a bucket map, as the server sent them.
        /// </summary>
        public ProfileTemplate BucketTemplate { get; }

        /// <summary>
        /// The name of the captured bucket.
        /// </summary>
        public string BucketName => ProfileJson.Text(BucketTemplate.Source["name"]) ?? "default";

        public ProfileNodeShape NodeShape { get; }

        /// <summary>
        /// How the first stable bucket map of each node count spread its vbuckets, by node count.
        /// </summary>
        public IReadOnlyList<ProfileVBucketLayout> VBucketLayouts => NodeShape.VBucketLayouts;

        /// <summary>
        /// The paths of the SDK config classes that no captured map has.
        /// </summary>
        public IReadOnlyList<string> SdkPathsNotSent { get; }

        public IReadOnlyList<ProfileExample> Examples { get; }

        /// <summary>
        /// The whole profile.json, as a new copy.
        /// </summary>
        public JsonObject Json => JsonNode.Parse(_json)!.AsObject();

        /// <summary>
        /// Loads an embedded profile by name. Loaded profiles are cached.
        /// </summary>
        public static ClusterProfile Load(string name) => Loaded.GetOrAdd(name, Read);

        /// <summary>
        /// The layout for a node count, or null when the capture has no stable map with that many nodes.
        /// </summary>
        public ProfileVBucketLayout? VBucketLayout(int nodes) => VBucketLayouts.FirstOrDefault(l => l.Nodes == nodes);

        /// <summary>
        /// Every normalized JSON path of the captured maps of one kind, for example nodesExt[].services.kv.
        /// </summary>
        public IReadOnlyList<ProfileField> Fields(string kind) =>
            _fields.TryGetValue(kind, out var fields)
                ? fields
                : throw new ArgumentException($"The kind is {ClusterKind} or {BucketKind}, not {kind}.", nameof(kind));

        /// <summary>
        /// An example by file name, with or without the examples/ folder.
        /// </summary>
        public ProfileExample Example(string file) =>
            Examples.FirstOrDefault(e => e.File == file || e.FileName == file)
            ?? throw new ArgumentException($"Profile {Name} has no example {file}.", nameof(file));

        public override string ToString() => Name;

        internal string ReadResource(string relativePath) =>
            ReadText(ResourcePrefix + _resourceFolder + "." + relativePath.Replace('/', '.'))
            ?? throw new InvalidOperationException($"Profile {Name} has no embedded file {relativePath}.");

        private static ClusterProfile Read(string name)
        {
            // MSBuild turns a dash in a folder name into an underscore in the resource name.
            var folder = name.Replace('-', '_');
            var json = ReadText(ResourcePrefix + folder + ProfileFile)
                       ?? throw new ArgumentException($"There is no profile {name}. Profiles are {string.Join(", ", Names)}.", nameof(name));
            return new ClusterProfile(folder, json);
        }

        private static IReadOnlyList<string> FindNames() =>
            typeof(ClusterProfile).Assembly.GetManifestResourceNames()
                .Where(r => r.StartsWith(ResourcePrefix, StringComparison.Ordinal) && r.EndsWith(ProfileFile, StringComparison.Ordinal))
                .Select(r => r.Substring(ResourcePrefix.Length, r.Length - ResourcePrefix.Length - ProfileFile.Length))
                .Where(folder => folder.IndexOf('.') < 0)
                .Select(folder => new ClusterProfile(folder, ReadText(ResourcePrefix + folder + ProfileFile)!))
                .Select(profile => Loaded.GetOrAdd(profile.Name, profile).Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

        private static string? ReadText(string resource)
        {
            using var stream = typeof(ClusterProfile).Assembly.GetManifestResourceStream(resource);
            if (stream is null) return null;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// The constant fields of one map kind, in the order the server writes the top level keys.
    /// </summary>
    internal sealed class ProfileTemplate
    {
        private readonly Dictionary<string, string> _patterns = new(StringComparer.Ordinal);

        internal ProfileTemplate(string kind, JsonObject json)
        {
            Kind = kind;
            KeyOrder = ProfileJson.Strings(json["keyOrder"]);
            VBucketServerMapKeyOrder = ProfileJson.Strings(json["vBucketServerMapKeyOrder"]);
            Source = json["body"]!.AsObject();
            foreach (var pattern in json["patterns"]?.AsObject() ?? new JsonObject())
            {
                // A path with several forms lists them all. The first one is enough to build a map.
                var value = pattern.Value is JsonArray forms ? forms.FirstOrDefault() : pattern.Value;
                if (ProfileJson.Text(value) is { } form) _patterns[pattern.Key] = form;
            }
        }

        public string Kind { get; }

        public IReadOnlyList<string> KeyOrder { get; }

        public IReadOnlyList<string> VBucketServerMapKeyOrder { get; }

        /// <summary>
        /// The constant fields, as a new copy. Fields that vary, such as rev and the nodes, are not in it.
        /// </summary>
        public JsonObject Body => Source.DeepClone().AsObject();

        /// <summary>
        /// String fields built from the bucket name, the bucket uuid or a node host name, by path.
        /// For example "uri" is "/pools/default/buckets/{name}?bucket_uuid={uuid}".
        /// </summary>
        public IReadOnlyDictionary<string, string> Patterns => _patterns;

        internal JsonObject Source { get; }

        public string? Pattern(string path) => _patterns.TryGetValue(path, out var pattern) ? pattern : null;
    }

    /// <summary>
    /// What the nodes of the captured cluster look like. A member is one node with one nodeUUID, in join order.
    /// </summary>
    internal sealed class ProfileNodeShape
    {
        private readonly Dictionary<string, int> _ports = new(StringComparer.Ordinal);

        internal ProfileNodeShape(JsonObject json)
        {
            NodesExtKeyOrder = ProfileJson.Strings(json["nodesExtKeyOrder"]);
            foreach (var port in json["ports"]?.AsObject() ?? new JsonObject())
            {
                // A key with several values lists them all. The first one is enough to build a map.
                var value = port.Value is JsonArray values ? values.FirstOrDefault() : port.Value;
                if (ProfileJson.Number(value) is { } number) _ports[port.Key] = number;
            }

            ServiceSets = ProfileJson.Items(json["serviceSets"]).Select(s => new ProfileServiceSet(s!.AsObject())).ToList();
            Members = ProfileJson.Items(json["members"]).Select(m => new ProfileMember(m!.AsObject())).ToList();
            var groups = json["serverGroups"];
            ServerGroupPattern = ProfileJson.Text(groups?["pattern"]);
            ServerGroups = ProfileJson.Strings(groups?["distinct"]);
            ServerGroupPerMember = ProfileJson.Items(groups?["perMember"]).Select(ProfileJson.Text).ToList();
            AlternateAddresses = (json["alternateAddresses"]?.AsObject() ?? new JsonObject())
                .Select(n => new ProfileAlternateNetwork(n.Key, n.Value!.AsObject()))
                .ToList();
            if (json["nodes"] is JsonObject nodes) LegacyNodes = new ProfileLegacyNodes(nodes);
            VBucketLayouts = ProfileJson.Items(json["vbucketLayouts"]).Select(l => new ProfileVBucketLayout(l!.AsObject())).ToList();
        }

        public IReadOnlyList<string> NodesExtKeyOrder { get; }

        /// <summary>
        /// The port of each nodesExt services key, for example kvSSL is 11207.
        /// </summary>
        public IReadOnlyDictionary<string, int> Ports => _ports;

        public IReadOnlyList<ProfileServiceSet> ServiceSets { get; }

        public IReadOnlyList<ProfileMember> Members { get; }

        /// <summary>
        /// The server group names with the number replaced by {n}, for example group:{n}.
        /// </summary>
        public string? ServerGroupPattern { get; }

        public IReadOnlyList<string> ServerGroups { get; }

        public IReadOnlyList<string?> ServerGroupPerMember { get; }

        public IReadOnlyList<ProfileAlternateNetwork> AlternateAddresses { get; }

        /// <summary>
        /// The shape of the entries of the bucket nodes[] list.
        /// </summary>
        public ProfileLegacyNodes? LegacyNodes { get; }

        public IReadOnlyList<ProfileVBucketLayout> VBucketLayouts { get; }

        /// <summary>
        /// The service of a port key, for example kvSSL is kv and indexHttps is index.
        /// </summary>
        public static string ServiceOf(string key)
        {
            var end = 0;
            while (end < key.Length && (key[end] is >= 'a' and <= 'z' || char.IsDigit(key[end]))) end++;
            return end == 0 ? key : key.Substring(0, end);
        }

        /// <summary>
        /// The node UUID of a member. Members past the capture follow the same fake UUID scheme.
        /// </summary>
        public string NodeUuid(int member) =>
            member < Members.Count && Members[member].NodeUuid is { } uuid ? uuid : "a" + member.ToString("x31");

        /// <summary>
        /// The server group of a member. Members past the capture take the groups in turn.
        /// </summary>
        public string? ServerGroup(int member)
        {
            if (member < ServerGroupPerMember.Count) return ServerGroupPerMember[member];
            return ServerGroups.Count == 0 ? null : ServerGroups[member % ServerGroups.Count];
        }
    }

    internal sealed class ProfileServiceSet
    {
        internal ProfileServiceSet(JsonObject json)
        {
            Id = ProfileJson.Number(json["id"]) ?? 0;
            Services = ProfileJson.Strings(json["services"]);
            Keys = ProfileJson.Strings(json["keys"]);
        }

        public int Id { get; }

        /// <summary>
        /// Service names, for example capi, kv, mgmt and projector for a data only node.
        /// </summary>
        public IReadOnlyList<string> Services { get; }

        /// <summary>
        /// The nodesExt services keys, for example kv and kvSSL.
        /// </summary>
        public IReadOnlyList<string> Keys { get; }
    }

    internal sealed class ProfileMember
    {
        internal ProfileMember(JsonObject json)
        {
            Index = ProfileJson.Number(json["index"]) ?? 0;
            Hostname = ProfileJson.Text(json["hostname"]);
            NodeUuid = ProfileJson.Text(json["nodeUUID"]);
            ServerGroup = ProfileJson.Text(json["serverGroup"]);
            ServiceSets = ProfileJson.Items(json["serviceSets"]).Select(s => ProfileJson.Number(s) ?? 0).ToList();
            Joined = json["joined"]?.GetValue<bool>() ?? false;
            Left = json["left"]?.GetValue<bool>() ?? false;
        }

        public int Index { get; }

        public string? Hostname { get; }

        public string? NodeUuid { get; }

        public string? ServerGroup { get; }

        /// <summary>
        /// The service set ids of the member over its life, in order.
        /// </summary>
        public IReadOnlyList<int> ServiceSets { get; }

        public bool Joined { get; }

        public bool Left { get; }
    }

    /// <summary>
    /// One alternate network of nodesExt, for example external for Private Link.
    /// </summary>
    internal sealed class ProfileAlternateNetwork
    {
        internal ProfileAlternateNetwork(string network, JsonObject json)
        {
            Network = network;
            SharedHostname = ProfileJson.Text(json["sharedHostname"]);
            PerNodeHostnames = ProfileJson.Items(json["perNode"]).Select(ProfileJson.Text).ToList();
            PortsFollowServices = json["portsFollowServices"]?.GetValue<bool>() ?? false;
            KeyOrder = ProfileJson.Strings(json["keyOrder"]);
            Ports = (json["ports"]?.AsObject() ?? new JsonObject())
                .ToDictionary(p => p.Key, p => new ProfileAlternatePort(p.Value!.AsObject()), StringComparer.Ordinal);
        }

        public string Network { get; }

        /// <summary>
        /// The host name of every member, or null when each member has its own.
        /// </summary>
        public string? SharedHostname { get; }

        public IReadOnlyList<string?> PerNodeHostnames { get; }

        /// <summary>
        /// True when a node lists an alternate port exactly when its services list the same key.
        /// </summary>
        public bool PortsFollowServices { get; }

        public IReadOnlyList<string> KeyOrder { get; }

        public IReadOnlyDictionary<string, ProfileAlternatePort> Ports { get; }

        public string? Hostname(int member) =>
            SharedHostname ?? (member < PerNodeHostnames.Count ? PerNodeHostnames[member] : null);
    }

    internal sealed class ProfileAlternatePort
    {
        internal ProfileAlternatePort(JsonObject json)
        {
            Shared = ProfileJson.Number(json["shared"]);
            PerNode = ProfileJson.Items(json["perNode"]).Select(ProfileJson.Number).ToList();
            ContiguousFrom = ProfileJson.Number(json["contiguousFrom"]);
        }

        public int? Shared { get; }

        public IReadOnlyList<int?> PerNode { get; }

        /// <summary>
        /// The lowest port when the members use a range with no gap.
        /// </summary>
        public int? ContiguousFrom { get; }

        /// <summary>
        /// The port of a member. Members past the capture continue the range.
        /// </summary>
        public int? Value(int member)
        {
            if (Shared is not null) return Shared;
            if (member < PerNode.Count) return PerNode[member];
            return ContiguousFrom + member;
        }
    }

    /// <summary>
    /// The shape of a bucket nodes[] entry.
    /// </summary>
    internal sealed class ProfileLegacyNodes
    {
        internal ProfileLegacyNodes(JsonObject json)
        {
            KeyOrder = ProfileJson.Strings(json["keyOrder"]);
            Source = json["entry"] as JsonObject ?? new JsonObject();
            AlternateAddresses = (json["alternateAddresses"]?.AsObject() ?? new JsonObject())
                .Select(n => new ProfileLegacyAlternate(n.Key, n.Value!.AsObject()))
                .ToList();
        }

        public IReadOnlyList<string> KeyOrder { get; }

        /// <summary>
        /// The constant part of an entry, as a new copy.
        /// </summary>
        public JsonObject Entry => Source.DeepClone().AsObject();

        public IReadOnlyList<ProfileLegacyAlternate> AlternateAddresses { get; }

        internal JsonObject Source { get; }
    }

    internal sealed class ProfileLegacyAlternate
    {
        internal ProfileLegacyAlternate(string network, JsonObject json)
        {
            Network = network;
            SharedHostname = ProfileJson.Text(json["sharedHostname"]);
            PortKeys = ProfileJson.Strings(json["portKeys"]);
        }

        public string Network { get; }

        public string? SharedHostname { get; }

        public IReadOnlyList<string> PortKeys { get; }
    }

    /// <summary>
    /// How a stable bucket map spread its vbuckets. Counts are sorted from low to high, so they do not say which node has which count.
    /// </summary>
    internal sealed class ProfileVBucketLayout
    {
        internal ProfileVBucketLayout(JsonObject json)
        {
            Nodes = ProfileJson.Number(json["nodes"]) ?? 0;
            Rev = ProfileJson.Number(json["rev"]) ?? 0;
            VBuckets = ProfileJson.Number(json["vbuckets"]) ?? 0;
            Copies = ProfileJson.Number(json["copies"]) ?? 0;
            ActivePerNode = ProfileJson.Items(json["activePerNode"]).Select(c => ProfileJson.Number(c) ?? 0).ToList();
            ReplicaPerNode = ProfileJson.Items(json["replicaPerNode"]).Select(c => ProfileJson.Number(c) ?? 0).ToList();
            ChainsWithMissingCopy = ProfileJson.Number(json["chainsWithMissingCopy"]) ?? 0;
            ChainsWithRepeatedNode = ProfileJson.Number(json["chainsWithRepeatedNode"]) ?? 0;
            ActiveRuns = ProfileJson.Number(json["activeRuns"]) ?? 0;
        }

        public int Nodes { get; }

        public int Rev { get; }

        public int VBuckets { get; }

        /// <summary>
        /// The active copy and the replicas of each vbucket.
        /// </summary>
        public int Copies { get; }

        public IReadOnlyList<int> ActivePerNode { get; }

        public IReadOnlyList<int> ReplicaPerNode { get; }

        public int ChainsWithMissingCopy { get; }

        public int ChainsWithRepeatedNode { get; }

        /// <summary>
        /// The runs of the same active node in vbucket order. Contiguous blocks give the node count, round robin gives the vbucket count.
        /// </summary>
        public int ActiveRuns { get; }

        public override string ToString() => $"{Nodes} nodes";
    }

    /// <summary>
    /// One normalized JSON path of the captured maps of one kind.
    /// </summary>
    internal sealed class ProfileField
    {
        internal ProfileField(string kind, JsonObject json)
        {
            Kind = kind;
            Path = ProfileJson.Text(json["path"]) ?? string.Empty;
            Types = ProfileJson.Strings(json["types"]);
            Scope = ProfileJson.Text(json["scope"]) ?? string.Empty;
            Share = json["share"]?.GetValue<double>() ?? 0;
            ModeledBySdk = json["modeledBySdk"]?.GetValue<bool>() ?? false;
        }

        public string Kind { get; }

        public string Path { get; }

        public IReadOnlyList<string> Types { get; }

        /// <summary>
        /// constant, perNode or perVersion.
        /// </summary>
        public string Scope { get; }

        /// <summary>
        /// The share of bodies that have the path.
        /// </summary>
        public double Share { get; }

        /// <summary>
        /// True when the SDK config classes have a property for the path.
        /// </summary>
        public bool ModeledBySdk { get; }

        public override string ToString() => $"{Kind} {Path}";
    }

    /// <summary>
    /// A real map of the capture, kept as one example of a shape.
    /// </summary>
    internal sealed class ProfileExample
    {
        private readonly ClusterProfile _profile;
        private string? _json;

        internal ProfileExample(ClusterProfile profile, JsonObject json)
        {
            _profile = profile;
            File = ProfileJson.Text(json["file"]) ?? string.Empty;
            Kind = ProfileJson.Text(json["kind"]) ?? string.Empty;
            Rev = (ulong)(ProfileJson.Number(json["rev"]) ?? 0);
            NodesExt = ProfileJson.Number(json["nodesExt"]) ?? 0;
            Nodes = ProfileJson.Number(json["nodes"]) ?? 0;
            ForwardMap = json["forwardMap"]?.GetValue<bool>() ?? false;
            ThisNode = json["thisNode"]?.GetValue<bool>() ?? false;
            AlternateAddresses = json["alternateAddresses"]?.GetValue<bool>() ?? false;
            UniformServices = json["uniformServices"]?.GetValue<bool>() ?? false;
            NodesMatchNodesExt = json["nodesMatchNodesExt"]?.GetValue<bool>() ?? false;
        }

        /// <summary>
        /// The path in the profile folder, for example examples/bucket-3nodes-stable.json.
        /// </summary>
        public string File { get; }

        public string FileName => File.Substring(File.LastIndexOf('/') + 1);

        public string Kind { get; }

        public ulong Rev { get; }

        public int NodesExt { get; }

        public int Nodes { get; }

        public bool ForwardMap { get; }

        public bool ThisNode { get; }

        public bool AlternateAddresses { get; }

        public bool UniformServices { get; }

        /// <summary>
        /// False when nodesExt lists a data node that is not in the bucket nodes[] list.
        /// </summary>
        public bool NodesMatchNodesExt { get; }

        /// <summary>
        /// The map as the server sent it, after the reducer replaced host names and UUIDs.
        /// </summary>
        public string Json => _json ??= _profile.ReadResource(File);

        public override string ToString() => $"{_profile.Name}/{File}";
    }

    internal static class ProfileJson
    {
        public static IEnumerable<JsonNode?> Items(JsonNode? node) => node as JsonArray ?? Enumerable.Empty<JsonNode?>();

        public static IReadOnlyList<string> Strings(JsonNode? node) => Items(node).Select(Text).OfType<string>().ToList();

        public static string? Text(JsonNode? node) =>
            node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        public static int? Number(JsonNode? node) =>
            node is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

        /// <summary>
        /// The keys of the profile order first, then the other keys in their own order.
        /// </summary>
        public static IEnumerable<string> InOrder(IReadOnlyList<string>? profileOrder, IEnumerable<string> keys) =>
            profileOrder is null ? keys : profileOrder.Concat(keys.Where(k => !profileOrder.Contains(k)));
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
