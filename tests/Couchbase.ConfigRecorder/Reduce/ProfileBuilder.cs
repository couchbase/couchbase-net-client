using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Couchbase.ConfigRecorder.Reduce;

/// <summary>
/// Reads the run folders of one cluster type and builds the profile and its examples.
/// Bodies are read in the order (run, version, first arrival, hash), so the output does not depend on file
/// system order or on the order of the folders on the command line.
/// </summary>
internal sealed partial class ProfileBuilder
{
    public const int MaxExamples = 10;

    private static readonly string[] Kinds = ["cluster", "bucket"];
    private static readonly string[] NotInTemplate =
    [
        "rev", "nodesExt", "nodes", "vBucketServerMap.serverList", "vBucketServerMap.vBucketMap", "vBucketServerMap.vBucketMapForward",
    ];

    private readonly string _name;
    private readonly string? _serverVersion;
    private readonly List<RunFolder> _runs;
    private readonly Anonymizer _anonymizer = new();
    private readonly SdkPaths _sdk = SdkPaths.ForBucketConfig();
    private readonly Dictionary<string, FieldStats> _fields = Kinds.ToDictionary(k => k, _ => new FieldStats());
    private readonly Dictionary<string, KeyOrder> _topKeys = Kinds.ToDictionary(k => k, _ => new KeyOrder());
    private readonly KeyOrder _mapKeys = new();
    private readonly NodeShape _nodeShape = new();
    private readonly Transitions _transitions = new();
    private readonly Dictionary<string, BodyInfo> _bodies = new(StringComparer.Ordinal);
    private readonly List<BodyInfo> _bodyOrder = [];
    private readonly Dictionary<string, (string Network, HashSet<string> Nodes)> _alternateHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _alternateOrder = [];
    private readonly Dictionary<(int Run, string Kind), Dictionary<MapVersion, double>> _earliest = new();
    private readonly Dictionary<(int Run, string Kind, MapVersion Version), string> _firstBody = new();
    private readonly List<string> _unreadable = [];
    private readonly SortedDictionary<int, JsonObject> _vbucketLayouts = new();
    private readonly Dictionary<int, (int NoActive, int MissingCopy)> _layoutGaps = new();
    private string? _bucketName;
    private string? _bucketUuid;

    public sealed record Example(string File, JsonNode Body);

    public sealed record Result(JsonObject Profile, IReadOnlyList<Example> Examples, IReadOnlyList<string> Fragments, IReadOnlyList<string> Warnings);

    private sealed record BodyInfo(
        string Hash,
        int Run,
        string Kind,
        MapVersion Version,
        string? ThisNodeHost,
        int NodesExt,
        int Nodes,
        bool Forward,
        bool Alternates,
        bool UniformServices,
        bool NodesMatch,
        IReadOnlyList<(string Host, long Port, string Node)> Endpoints,
        IReadOnlySet<string> NodesExtHosts)
    {
        public bool HasThisNode => ThisNodeHost is not null;

        public bool Stable => !Forward && HasThisNode && UniformServices && NodesMatch;
    }

    public ProfileBuilder(string name, string? serverVersion, IEnumerable<RunFolder> runs)
    {
        _name = name;
        _serverVersion = serverVersion;
        _runs = runs.OrderBy(r => Path.GetFileName(Path.TrimEndingDirectorySeparator(r.Folder)), StringComparer.Ordinal).ToList();
    }

    public Result Build()
    {
        for (var run = 0; run < _runs.Count; run++)
        {
            ReadRun(run);
        }

        foreach (var host in _alternateOrder)
        {
            var (network, nodes) = _alternateHosts[host];
            _anonymizer.AddAlternateHost(host, network, nodes);
        }

        AddOtherHosts();

        var missing = MissingThisNode();
        var examples = PickExamples();
        var profile = new JsonObject
        {
            ["name"] = _name,
            ["source"] = SourceJson(),
            ["template"] = TemplateJson(),
            ["nodeShape"] = NodeShapeJson(),
            ["fields"] = FieldsJson(),
            ["transitions"] = TransitionsJson(missing),
            ["examples"] = new JsonArray(examples.Select(e => (JsonNode)e.Entry).ToArray()),
        };

        var warnings = _unreadable.Select(u => $"Skipped a body that is missing or not valid JSON: {u}").ToList();
        return new Result(profile, examples.Select(e => e.Example).ToList(), _anonymizer.Fragments(JsonKeys()), warnings);
    }

    /// <summary>
    /// Every key name of the bodies and of the SDK paths.
    /// </summary>
    private HashSet<string> JsonKeys() =>
        _fields.Values.SelectMany(f => f.Paths).Concat(_sdk.All)
            .SelectMany(p => p.Split('.', '[', ']', '*'))
            .Where(k => k.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void ReadRun(int run)
    {
        var folder = _runs[run];
        foreach (var kind in Kinds)
        {
            var earliest = new Dictionary<MapVersion, double>();
            foreach (var arrival in folder.Arrivals(kind).Where(a => a.Source != "dispatched"))
            {
                if (!earliest.TryGetValue(arrival.Version, out var t) || arrival.T < t) earliest[arrival.Version] = arrival.T;
            }

            _earliest[(run, kind)] = earliest;
        }

        var arrivals = BodyEvents(folder)
            .GroupBy(e => e.Str("hash")!)
            .Select(g => (Hash: g.Key, Version: new MapVersion(g.First().U64("bodyRevEpoch") ?? 0, g.First().U64("bodyRev") ?? 0), T: g.Min(e => e.T)))
            .OrderBy(a => a.Version)
            .ThenBy(a => a.T)
            .ThenBy(a => a.Hash, StringComparer.Ordinal)
            .ToList();

        var lastVersion = new Dictionary<string, MapVersion>();
        MapVersion? firstVersion = null;
        foreach (var (hash, _, _) in arrivals)
        {
            ConfigBody body;
            try
            {
                body = ConfigBody.Parse(hash, File.ReadAllBytes(folder.BodyPath(hash)));
            }
            catch (Exception)
            {
                _unreadable.Add($"{RunName(folder)}/configs/{hash}.json");
                continue;
            }

            if (!_bodies.ContainsKey(hash)) ObserveDistinct(run, body);

            if (lastVersion.TryGetValue(body.Kind, out var last) && body.Version.CompareTo(last) <= 0) continue;
            lastVersion[body.Kind] = body.Version;
            _firstBody[(run, body.Kind, body.Version)] = hash;
            firstVersion ??= body.Version;
            _transitions.Add(run, body, _earliest[(run, body.Kind)].TryGetValue(body.Version, out var t) ? t : null);
            _nodeShape.ObserveVersion(run, body.Version.Equals(firstVersion.Value), body);
        }

        _nodeShape.EndRun(run);
    }

    private static IEnumerable<RunEvent> BodyEvents(RunFolder folder) =>
        folder.Events.Where(e => e.Stream is "tap" or "probe-tap" && e.Kind is "config" or "nmvb"
                                 && e.Str("hash") is not null && e.U64("bodyRev") is not null);

    private void ObserveDistinct(int run, ConfigBody body)
    {
        foreach (var node in body.NodesExt)
        {
            if (node.Host is not null) _anonymizer.AddNodeHost(node.Host);
        }

        foreach (var node in body.Nodes)
        {
            _anonymizer.AddNodeHost(node.Host);
        }

        foreach (var server in body.ServerList)
        {
            _anonymizer.AddNodeHost(ConfigBody.HostOf(server));
        }

        if (body.ClusterUuid is not null) _anonymizer.AddUuid(body.ClusterUuid, 'c');
        if (body.Kind == "bucket" && body.Uuid is not null)
        {
            _anonymizer.AddUuid(body.Uuid, 'b');
            _bucketUuid ??= body.Uuid;
            _bucketName ??= body.Name;
        }

        foreach (var node in body.NodesExt)
        {
            if (node.Uuid is not null) _anonymizer.AddUuid(node.Uuid, 'a');
        }

        var endpoints = new List<(string Host, long Port, string Node)>();
        foreach (var node in body.NodesExt.Where(n => n.Host is not null))
        {
            foreach (var key in new[] { "kv", "kvSSL" })
            {
                if (node.Port(key) is { } port) endpoints.Add((node.Host!, port, node.Host!));
            }

            foreach (var alternate in node.Alternates.Where(a => a.Host is not null))
            {
                if (!_alternateHosts.TryGetValue(alternate.Host!, out var entry))
                {
                    entry = (alternate.Network, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    _alternateHosts[alternate.Host!] = entry;
                    _alternateOrder.Add(alternate.Host!);
                }

                entry.Nodes.Add(node.Host!);
                foreach (var port in alternate.Ports.Where(p => p.Key is "kv" or "kvSSL"))
                {
                    endpoints.Add((alternate.Host!, port.Port, node.Host!));
                }
            }
        }

        _fields[body.Kind].Add(body);
        _topKeys[body.Kind].Merge(body.TopKeys);
        _mapKeys.Merge(body.MapKeys);
        _nodeShape.Observe(body);

        var info = new BodyInfo(body.Hash, run, body.Kind, body.Version, body.NodesExt.FirstOrDefault(n => n.ThisNode)?.Host ?? (body.HasThisNode ? "" : null),
            body.NodesExt.Count, body.Nodes.Count, body.HasForward, body.HasAlternates, body.UniformServices, body.NodesMatch, endpoints,
            body.NodesExt.Select(n => n.Host ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase));
        _bodies[body.Hash] = info;
        _bodyOrder.Add(info);
        if (info.Kind == "bucket" && info.Stable && body.Map is not null)
        {
            // A failover leaves stable maps with lost copies, so keep the most complete map per node count.
            var gaps = Gaps(body.Map);
            if (!_layoutGaps.TryGetValue(info.NodesExt, out var kept) || gaps.CompareTo(kept) < 0)
            {
                _vbucketLayouts[info.NodesExt] = VBucketLayout(body);
                _layoutGaps[info.NodesExt] = gaps;
            }
        }
    }

    private static (int NoActive, int MissingCopy) Gaps(string[][] map) =>
        (map.Count(c => c.FirstOrDefault() is null or ""), map.Count(c => c.Contains("")));

    /// <summary>
    /// How a stable bucket map spreads vbuckets over the nodes. activeRuns counts the runs of the same
    /// active node in vbucket order, so it is the node count for contiguous blocks and 1024 for round robin.
    /// </summary>
    private static JsonObject VBucketLayout(ConfigBody body)
    {
        var map = body.Map!;
        var hosts = body.ServerList.Select(ConfigBody.HostOf).ToList();
        int[] Count(Func<string[], IEnumerable<string>> copies) =>
            hosts.Select(h => map.Count(chain => copies(chain).Contains(h))).Order().ToArray();

        var runs = 0;
        for (var i = 0; i < map.Length; i++)
        {
            if (i == 0 || map[i].FirstOrDefault() != map[i - 1].FirstOrDefault()) runs++;
        }

        return new JsonObject
        {
            ["nodes"] = body.NodesExt.Count,
            ["rev"] = body.Version.Rev,
            ["vbuckets"] = map.Length,
            ["copies"] = map.Length == 0 ? 0 : map[0].Length,
            ["activePerNode"] = new JsonArray(Count(c => c.Take(1)).Select(n => (JsonNode)n).ToArray()),
            ["replicaPerNode"] = new JsonArray(Count(c => c.Skip(1)).Select(n => (JsonNode)n).ToArray()),
            ["chainsWithMissingCopy"] = map.Count(c => c.Contains("")),
            ["chainsWithRepeatedNode"] = map.Count(c => c.Where(h => h.Length > 0).Distinct().Count() < c.Count(h => h.Length > 0)),
            ["activeRuns"] = runs,
        };
    }

    private static string RunName(RunFolder folder) => Path.GetFileName(Path.TrimEndingDirectorySeparator(folder.Folder));

    /// <summary>
    /// The node shape, plus the constant part of a bucket nodes[] entry and the vbucket layouts.
    /// </summary>
    private JsonObject NodeShapeJson()
    {
        var shape = _nodeShape.ToJson(_anonymizer);
        var bucket = _bodyOrder.FirstOrDefault(b => b.Kind == "bucket" && b.Nodes > 0);
        if (bucket is not null && shape["nodes"] is JsonObject nodes
                               && ReadAnonymized(bucket)["nodes"] is JsonArray { Count: > 0 } entries)
        {
            var entry = entries[0]!.DeepClone();
            PruneToConstant(entry, "nodes[]", _fields["bucket"]);
            nodes["entry"] = entry;
        }

        shape["vbucketLayouts"] = new JsonArray(_vbucketLayouts.Values.Select(v => (JsonNode)v.DeepClone()).ToArray());
        return shape;
    }

    /// <summary>
    /// Host names from the events, such as seeds, DNS SRV targets and socket addresses. No body holds them,
    /// but the leak scan looks for them.
    /// </summary>
    private void AddOtherHosts()
    {
        foreach (var run in _runs)
        {
            foreach (var e in run.Events)
            {
                if (e.Str("host") is { } host) _anonymizer.AddOtherHost(host);
                if (e.Str("node") is { } node) _anonymizer.AddOtherHost(ConfigBody.HostOf(node));
                foreach (var field in new[] { "remote", "local" })
                {
                    if (e.Str(field) is { } address) _anonymizer.AddOtherHost(ConfigBody.HostOf(address));
                }

                if (e.Stream == "dns" && e.E.TryGetProperty("records", out var records))
                {
                    foreach (var record in records.EnumerateArray())
                    {
                        if (record.TryGetProperty("target", out var target) && target.GetString() is { } t) _anonymizer.AddOtherHost(t);
                    }
                }

                if (e.Stream == "recorder" && e.Kind == "start" && e.E.TryGetProperty("settings", out var settings)
                    && settings.TryGetProperty("connectionString", out var connection) && connection.GetString() is { } connectionString)
                {
                    foreach (var seed in SeedHosts(connectionString))
                    {
                        _anonymizer.AddOtherHost(seed);
                    }
                }
            }
        }
    }

    private static IEnumerable<string> SeedHosts(string connectionString)
    {
        var rest = connectionString.Contains("://", StringComparison.Ordinal)
            ? connectionString[(connectionString.IndexOf("://", StringComparison.Ordinal) + 3)..]
            : connectionString;
        rest = rest.Split('?')[0];
        return rest.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ConfigBody.HostOf);
    }

    /// <summary>
    /// Bodies without thisNode, and the node that sent each one. A connection talks to one node, so the
    /// node comes from another body on the same connection that has thisNode. When there is none, the host
    /// and port of the connection are looked up in the node and alternate addresses of the body.
    /// </summary>
    private Dictionary<string, JsonArray> MissingThisNode()
    {
        var result = Kinds.ToDictionary(k => k, _ => new JsonArray());
        var seen = new HashSet<(int, string, string)>();
        for (var run = 0; run < _runs.Count; run++)
        {
            var events = BodyEvents(_runs[run]).Where(e => _bodies.ContainsKey(e.Str("hash")!)).ToList();
            var byConnection = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in events)
            {
                var info = _bodies[e.Str("hash")!];
                if (info.ThisNodeHost is { Length: > 0 } node && e.Str("cid") is { } cid) byConnection.TryAdd(cid, node);
            }

            foreach (var e in events)
            {
                var info = _bodies[e.Str("hash")!];
                if (info.HasThisNode) continue;
                var host = e.Str("host");
                var port = (long?)e.U64("port");
                string? node = e.Str("cid") is { } cid && byConnection.TryGetValue(cid, out var byCid) ? byCid
                    : info.Endpoints.FirstOrDefault(p => string.Equals(p.Host, host, StringComparison.OrdinalIgnoreCase) && p.Port == port).Node;
                var servedBy = node is null ? "unknown" : _anonymizer.Host(node);
                if (!seen.Add((run, info.Hash, servedBy))) continue;

                result[info.Kind].Add(new JsonObject
                {
                    ["run"] = run,
                    ["rev"] = info.Version.Rev,
                    ["servedBy"] = servedBy,
                    ["servedByInNodesExt"] = node is not null && info.NodesExtHosts.Contains(node),
                    ["source"] = $"{e.Stream} {e.Kind}",
                    ["firstBodyOfVersion"] = _firstBody.GetValueOrDefault((run, info.Kind, info.Version)) == info.Hash,
                    ["atSeconds"] = Math.Round(e.T / 1000, 1),
                });
            }
        }

        return result;
    }

    private JsonObject SourceJson()
    {
        var runs = new JsonArray();
        foreach (var run in _runs)
        {
            var probeStart = run.Events.FirstOrDefault(e => e.Stream == "probe" && e.Kind == "start");
            runs.Add(new JsonObject
            {
                ["folder"] = RunName(run),
                ["tls"] = probeStart?.Bool("tls"),
                ["seconds"] = run.Events.Count == 0 ? 0 : Math.Round((run.Events[^1].T - run.Events[0].T) / 1000, 1),
            });
        }

        var firstTs = _runs.SelectMany(r => r.Events).Select(e => e.Ts).FirstOrDefault(ts => ts.Length >= 10);
        var bodies = new JsonObject();
        var versions = new JsonObject();
        foreach (var kind in Kinds)
        {
            bodies[kind] = _fields[kind].Bodies;
            versions[kind] = _transitions.Versions(kind);
        }

        return new JsonObject
        {
            ["serverVersion"] = _serverVersion,
            ["captureDate"] = firstTs?[..10],
            ["runs"] = runs,
            ["bodies"] = bodies,
            ["versionsWithBody"] = versions,
        };
    }

    private JsonObject TemplateJson()
    {
        var template = new JsonObject();
        foreach (var kind in Kinds)
        {
            var first = _bodyOrder.FirstOrDefault(b => b.Kind == kind);
            if (first is null) continue;

            var body = ReadAnonymized(first);
            var stats = _fields[kind];
            PruneToConstant(body, "", stats);
            var entry = new JsonObject { ["keyOrder"] = NodeShape.Strings(_topKeys[kind].Keys) };
            if (kind == "bucket") entry["vBucketServerMapKeyOrder"] = NodeShape.Strings(_mapKeys.Keys);
            entry["body"] = body;
            entry["patterns"] = PatternsJson(stats);
            var optional = stats.Paths.Where(p => stats.IsConstant(p) && stats.Get(p)!.Bodies < stats.Bodies)
                .Order(StringComparer.Ordinal).ToList();
            if (optional.Count > 0) entry["constantWhenPresent"] = NodeShape.Strings(optional);
            template[kind] = entry;
        }

        return template;
    }

    private JsonNode ReadAnonymized(BodyInfo info)
    {
        var node = JsonNode.Parse(File.ReadAllBytes(_runs[info.Run].BodyPath(info.Hash)))!;
        _anonymizer.Rewrite(node);
        return node;
    }

    /// <summary>
    /// Keeps the members whose paths have one value in every body of the kind.
    /// </summary>
    private static bool PruneToConstant(JsonNode? node, string path, FieldStats stats)
    {
        bool Keep(string p) => stats.IsConstant(p) && stats.Get(p)!.Bodies == stats.Bodies;

        switch (node)
        {
            case JsonObject obj:
                if (obj.Count == 0) return Keep(path);
                foreach (var name in obj.Select(p => p.Key).ToList())
                {
                    var child = path.Length == 0 ? name : $"{path}.{name}";
                    if (NotInTemplate.Contains(child) || !PruneToConstant(obj[name], child, stats)) obj.Remove(name);
                }

                return obj.Count > 0;
            case JsonArray array:
                if (array.Count == 0) return Keep(path);
                var prefix = path + "[]";
                return stats.Paths.Where(p => p == prefix || p.StartsWith(prefix + ".", StringComparison.Ordinal) || p.StartsWith(prefix + "[", StringComparison.Ordinal))
                    .All(Keep);
            default:
                return Keep(path);
        }
    }

    /// <summary>
    /// String fields built from the node host name, the bucket name or the bucket uuid.
    /// </summary>
    private JsonObject PatternsJson(FieldStats stats)
    {
        var result = new JsonObject();
        var bucketUuid = _bucketUuid is null ? null : _anonymizer.Uuid(_bucketUuid);
        foreach (var path in stats.Paths.Order(StringComparer.Ordinal))
        {
            var stat = stats.Get(path)!;
            if (!stat.Types.SetEquals(["string"])) continue;

            var patterns = FieldStats.Samples(stat.Values, _anonymizer)
                .Select(raw => JsonNode.Parse(raw)!.GetValue<string>())
                .Select(value => Pattern(value, _bucketName, bucketUuid))
                .Where(p => p.Changed)
                .Select(p => p.Pattern)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
            if (patterns.Count == 0 || patterns.Count > 3 || patterns.All(p => SinglePlaceholder().IsMatch(p))) continue;
            result[path] = patterns.Count == 1 ? patterns[0] : NodeShape.Strings(patterns);
        }

        return result;
    }

    private static (string Pattern, bool Changed) Pattern(string value, string? bucketName, string? bucketUuid)
    {
        var pattern = FakeNodeHost().Replace(value, "{hostname}");
        if (bucketUuid is not null) pattern = pattern.Replace(bucketUuid, "{uuid}", StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(bucketName))
        {
            // The pool is always named default, so /pools/default is not the bucket name.
            pattern = Regex.Replace(pattern, "(?<!/pools)/" + Regex.Escape(bucketName) + "(?=$|[/?]|%2B)", "/{name}");
        }

        return (pattern, pattern != value);
    }

    private JsonObject FieldsJson()
    {
        var fields = new JsonObject();
        var allPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in Kinds)
        {
            var stats = _fields[kind];
            fields[kind] = new JsonArray(stats.Paths.Order(StringComparer.Ordinal).Select(p => (JsonNode)stats.ToJson(p, _anonymizer, _sdk)).ToArray());
            allPaths.UnionWith(stats.Paths);
        }

        fields["sdkPathsNotSent"] = NodeShape.Strings(_sdk.NotIn(allPaths));
        return fields;
    }

    private JsonObject TransitionsJson(Dictionary<string, JsonArray> missing)
    {
        var result = new JsonObject();
        foreach (var kind in Kinds)
        {
            var seen = Enumerable.Range(0, _runs.Count)
                .Select(run => (run, _earliest[(run, kind)].OrderBy(p => p.Key).Select(p => (p.Key, p.Value)).ToList()))
                .ToList();
            result[kind] = _transitions.ToJson(kind, seen, missing[kind]);
        }

        return result;
    }

    /// <summary>
    /// One example per shape, in priority order, at most <see cref="MaxExamples"/>. Each shape lists its
    /// matches from best to worst, and takes the first body that no other shape took.
    /// </summary>
    private List<(Example Example, JsonObject Entry)> PickExamples()
    {
        var shapes = new List<(string Feature, string Kind, Func<BodyInfo, bool>[] Matches)>();
        foreach (var kind in Kinds)
        {
            if (_bodyOrder.FirstOrDefault(b => b.Kind == kind) is not { } initial) continue;
            shapes.Add(("stable", kind, [b => b.Stable && b.NodesExt == initial.NodesExt, b => b.Stable]));
        }

        var most = _bodyOrder.Where(b => b.Forward).Select(b => b.NodesExt).DefaultIfEmpty(0).Max();
        shapes.Add(("forward-map", "bucket", [b => b.Forward && b.NodesExt == most && b.HasThisNode, b => b.Forward]));
        shapes.Add(("no-this-node", "bucket", [b => !b.HasThisNode]));
        shapes.Add(("no-this-node", "cluster", [b => !b.HasThisNode]));
        shapes.Add(("mixed-services", "bucket",
            [b => !b.UniformServices && !b.Forward && b.HasThisNode && b.NodesMatch, b => !b.UniformServices && b.HasThisNode, b => !b.UniformServices]));
        shapes.Add(("node-outside-bucket", "bucket",
            [b => !b.NodesMatch && !b.Forward && b.HasThisNode && b.UniformServices, b => !b.NodesMatch && b.HasThisNode, b => !b.NodesMatch]));
        shapes.Add(("mixed-services", "cluster", [b => !b.UniformServices && b.HasThisNode, b => !b.UniformServices]));
        foreach (var kind in Kinds)
        {
            var ofKind = _bodyOrder.Where(b => b.Kind == kind).ToList();
            if (ofKind.Any(b => b.Alternates) && ofKind.Any(b => !b.Alternates))
            {
                shapes.Add(("alternate-addresses", kind, [b => b.Alternates && b.Stable, b => b.Alternates]));
            }
        }

        foreach (var kind in new[] { "bucket", "cluster" })
        {
            foreach (var count in _bodyOrder.Where(b => b.Kind == kind && b.Stable).Select(b => b.NodesExt).Distinct().OrderDescending())
            {
                shapes.Add(("stable", kind, [b => b.Stable && b.NodesExt == count]));
            }
        }

        var picked = new List<(Example, JsonObject)>();
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (feature, kind, matches) in shapes)
        {
            if (picked.Count == MaxExamples) break;
            var body = matches
                .Select(match => _bodyOrder.FirstOrDefault(b => b.Kind == kind && !hashes.Contains(b.Hash) && match(b)))
                .FirstOrDefault(b => b is not null);
            if (body is null) continue;
            var file = $"examples/{body.Kind}-{body.NodesExt}nodes-{feature}.json";
            if (!names.Add(file)) continue;
            hashes.Add(body.Hash);

            picked.Add((new Example(file, ReadAnonymized(body)), new JsonObject
            {
                ["file"] = file,
                ["kind"] = body.Kind,
                ["rev"] = body.Version.Rev,
                ["nodesExt"] = body.NodesExt,
                ["nodes"] = body.Nodes,
                ["forwardMap"] = body.Forward,
                ["thisNode"] = body.HasThisNode,
                ["alternateAddresses"] = body.Alternates,
                ["uniformServices"] = body.UniformServices,
                ["nodesMatchNodesExt"] = body.NodesMatch,
            }));
        }

        return picked;
    }

    [GeneratedRegex(@"node-\d+\.example\.com")]
    private static partial Regex FakeNodeHost();

    [GeneratedRegex(@"^\{[a-z]+\}$")]
    private static partial Regex SinglePlaceholder();
}
