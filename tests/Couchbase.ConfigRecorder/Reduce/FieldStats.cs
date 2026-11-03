using System.Globalization;
using System.Text.Json.Nodes;

namespace Couchbase.ConfigRecorder.Reduce;

/// <summary>
/// Counts each normalized JSON path of the bodies of one kind, and finds how its value varies.
/// </summary>
internal sealed class FieldStats
{
    public const int MaxSamples = 5;

    private static readonly (string Prefix, string List)[] NodeLists =
    [
        ("nodesExt[", "nodesExt"),
        ("nodes[", "nodes"),
        ("vBucketServerMap.serverList[", "serverList"),
    ];

    private readonly Dictionary<string, Stat> _stats = new(StringComparer.Ordinal);

    public int Bodies { get; private set; }

    public IReadOnlyCollection<string> Paths => _stats.Keys;

    public sealed class Stat
    {
        public int Bodies { get; set; }
        public SortedSet<string> Types { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Values { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// The nodes of one body disagree. Some have another value or no value.
        /// </summary>
        public bool PerNode { get; set; }

        public bool DiffersBetweenBodies { get; set; }
        public ulong? FirstSignature { get; set; }
    }

    public Stat? Get(string path) => _stats.GetValueOrDefault(path);

    public string Scope(string path)
    {
        var stat = _stats[path];
        return stat.PerNode ? "perNode" : stat.DiffersBetweenBodies || stat.Bodies < Bodies ? "perVersion" : "constant";
    }

    public bool IsConstant(string path) => _stats.ContainsKey(path) && Scope(path) == "constant";

    public void Add(ConfigBody body)
    {
        Bodies++;
        var lengths = new Dictionary<string, int>
        {
            ["nodesExt"] = body.NodesExt.Count,
            ["nodes"] = body.Nodes.Count,
            ["serverList"] = body.ServerList.Count,
        };

        var perBody = new Dictionary<string, PathInBody>(StringComparer.Ordinal);
        foreach (var (key, raw) in body.Flat)
        {
            var path = ConfigJson.Normalize(key);
            if (!_stats.TryGetValue(path, out var stat))
            {
                stat = new Stat();
                _stats[path] = stat;
            }

            stat.Types.Add(TypeOf(raw));
            stat.Values.Add(raw);
            if (!perBody.TryGetValue(path, out var inBody))
            {
                inBody = new PathInBody(NodeListOf(key));
                perBody[path] = inBody;
            }

            inBody.Add(key, raw);
        }

        foreach (var (path, inBody) in perBody)
        {
            var stat = _stats[path];
            stat.Bodies++;
            ulong signature;
            if (inBody.List is { } list)
            {
                var distinct = inBody.ByEntry.Values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                if (inBody.ByEntry.Count < lengths[list.List] || distinct.Count > 1) stat.PerNode = true;

                // A node list path keeps its scope when nodes join or leave, so only the distinct values count.
                signature = Fnv.Hash(distinct);
            }
            else
            {
                signature = inBody.Ordered;
            }

            if (stat.FirstSignature is null) stat.FirstSignature = signature;
            else if (stat.FirstSignature != signature) stat.DiffersBetweenBodies = true;
        }
    }

    /// <summary>
    /// The JSON object of one path for profile.json. Samples are anonymized.
    /// </summary>
    public JsonObject ToJson(string path, Anonymizer anonymizer, SdkPaths sdk)
    {
        var stat = _stats[path];
        var samples = new JsonArray();
        foreach (var sample in Samples(stat.Values, anonymizer).Take(MaxSamples))
        {
            samples.Add(JsonNode.Parse(sample));
        }

        return new JsonObject
        {
            ["path"] = path,
            ["types"] = new JsonArray(stat.Types.Select(t => (JsonNode)t).ToArray()),
            ["bodies"] = stat.Bodies,
            ["share"] = Math.Round((double)stat.Bodies / Bodies, 3),
            ["distinct"] = stat.Values.Count,
            ["samples"] = samples,
            ["scope"] = Scope(path),
            ["modeledBySdk"] = sdk.Models(path),
        };
    }

    /// <summary>
    /// Anonymized distinct values, numbers in numeric order and other values in ordinal order.
    /// </summary>
    public static List<string> Samples(IEnumerable<string> rawValues, Anonymizer anonymizer)
    {
        var values = rawValues.Select(raw => AnonymizeRaw(raw, anonymizer)).Distinct(StringComparer.Ordinal).ToList();
        return values
            .OrderBy(v => TypeOf(v) == "number" ? 0 : 1)
            .ThenBy(v => TypeOf(v) == "number" && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0)
            .ThenBy(v => v, StringComparer.Ordinal)
            .ToList();
    }

    public static string AnonymizeRaw(string raw, Anonymizer anonymizer) =>
        TypeOf(raw) == "string" ? JsonText.Inline(JsonValue.Create(anonymizer.Text(JsonNode.Parse(raw)!.GetValue<string>()))) : raw;

    public static string TypeOf(string raw) => raw switch
    {
        "{}" => "object",
        "[]" => "array",
        "true" or "false" => "boolean",
        "null" => "null",
        _ when raw.StartsWith('"') => "string",
        _ => "number",
    };

    private static (string Prefix, string List)? NodeListOf(string key)
    {
        foreach (var list in NodeLists)
        {
            if (key.StartsWith(list.Prefix, StringComparison.Ordinal)) return list;
        }

        return null;
    }

    private sealed class PathInBody((string Prefix, string List)? list)
    {
        private ulong _ordered = Fnv.Offset;

        public (string Prefix, string List)? List { get; } = list;

        public Dictionary<int, string> ByEntry { get; } = new();

        public ulong Ordered => _ordered;

        public void Add(string key, string raw)
        {
            _ordered = Fnv.Add(_ordered, raw);
            if (List is not { } nodeList) return;
            var close = key.IndexOf(']', nodeList.Prefix.Length);
            var entry = int.Parse(key.AsSpan(nodeList.Prefix.Length, close - nodeList.Prefix.Length), CultureInfo.InvariantCulture);
            ByEntry[entry] = ByEntry.TryGetValue(entry, out var existing) ? existing + "\u0001" + raw : raw;
        }
    }
}

/// <summary>
/// 64 bit FNV-1a. It only compares values inside one run of the tool and never reaches a file.
/// </summary>
internal static class Fnv
{
    public const ulong Offset = 14695981039346656037UL;

    public static ulong Add(ulong hash, string value)
    {
        foreach (var c in value)
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        return (hash ^ 1) * 1099511628211UL;
    }

    public static ulong Hash(IEnumerable<string> values) => values.Aggregate(Offset, Add);
}
