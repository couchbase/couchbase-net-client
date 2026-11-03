using System.Text.Json;

namespace Couchbase.ConfigRecorder.Reduce;

internal sealed record PortEntry(string Key, long Port);

internal sealed record AlternateAddress(string Network, string? Host, IReadOnlyList<PortEntry> Ports);

/// <summary>
/// One nodesExt entry. <see cref="Host"/> is null when the server leaves the hostname out.
/// </summary>
internal sealed class ExtNode
{
    public string? Host { get; init; }
    public string? Uuid { get; init; }
    public bool ThisNode { get; init; }
    public string? ServerGroup { get; init; }
    public IReadOnlyList<string> Keys { get; init; } = [];
    public IReadOnlyList<PortEntry> Services { get; init; } = [];
    public string ServicesRaw { get; init; } = "";
    public IReadOnlyList<AlternateAddress> Alternates { get; init; } = [];
    public string AlternatesRaw { get; init; } = "";

    /// <summary>
    /// Raw JSON of the other properties, without hostname and thisNode.
    /// </summary>
    public IReadOnlyDictionary<string, string> Other { get; init; } = new Dictionary<string, string>();

    public string ServiceSet => string.Join(",", Services.Select(s => s.Key));

    public long? Port(string key) => Services.FirstOrDefault(s => s.Key == key)?.Port;
}

/// <summary>
/// One entry of the legacy bucket nodes[] list. <see cref="Host"/> is the hostname without its port.
/// </summary>
internal sealed class LegacyNode
{
    public string Host { get; init; } = "";
    public IReadOnlyList<string> Keys { get; init; } = [];
    public IReadOnlyList<AlternateAddress> Alternates { get; init; } = [];

    /// <summary>
    /// Raw JSON of every property except hostname.
    /// </summary>
    public IReadOnlyDictionary<string, string> Other { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// The parts of a config body that the reducer compares. Cluster bodies have no name and no bucket parts.
/// </summary>
internal sealed class ConfigBody
{
    private ConfigBody()
    {
    }

    public required string Hash { get; init; }
    public required string Kind { get; init; }
    public required MapVersion Version { get; init; }
    public string? Name { get; private init; }
    public string? Uuid { get; private init; }
    public string? ClusterUuid { get; private init; }
    public IReadOnlyList<string> TopKeys { get; private init; } = [];
    public IReadOnlyList<string> MapKeys { get; private init; } = [];
    public IReadOnlyList<ExtNode> NodesExt { get; private init; } = [];
    public IReadOnlyList<LegacyNode> Nodes { get; private init; } = [];
    public IReadOnlyList<string> ServerList { get; private init; } = [];
    public string[][]? Map { get; private init; }
    public string[][]? Forward { get; private init; }
    public required Dictionary<string, string> Flat { get; init; }

    public bool HasThisNode => NodesExt.Any(n => n.ThisNode);

    public bool HasForward => Forward is not null;

    public bool HasAlternates => NodesExt.Any(n => n.Alternates.Count > 0);

    public bool UniformServices => NodesExt.Select(n => n.ServiceSet).Distinct().Count() <= 1;

    /// <summary>
    /// True when the bucket nodes[] list holds the same hosts as the data nodes of nodesExt.
    /// </summary>
    public bool NodesMatch =>
        Kind != "bucket" || Nodes.Select(n => n.Host).ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(NodesExt.Where(n => n.Port("kv") is not null || n.Port("kvSSL") is not null).Select(n => n.Host ?? ""));

    public static ConfigBody Parse(string hash, byte[] bytes)
    {
        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        var name = Str(root, "name");
        var version = new MapVersion(U64(root, "revEpoch") ?? 0, U64(root, "rev") ?? 0);
        var nodesExt = Array(root, "nodesExt").Select(ReadExtNode).ToList();
        var nodes = Array(root, "nodes").Select(ReadLegacyNode).ToList();
        var serverList = new List<string>();
        string[][]? map = null, forward = null;
        var mapKeys = new List<string>();
        if (root.TryGetProperty("vBucketServerMap", out var vbsm) && vbsm.ValueKind == JsonValueKind.Object)
        {
            mapKeys.AddRange(vbsm.EnumerateObject().Select(p => p.Name));
            serverList.AddRange(Array(vbsm, "serverList").Select(s => s.GetString() ?? ""));
            var hosts = serverList.Select(HostOf).ToList();
            map = Chains(vbsm, "vBucketMap", hosts);
            forward = Chains(vbsm, "vBucketMapForward", hosts);
        }

        return new ConfigBody
        {
            Hash = hash,
            Kind = name is null ? "cluster" : "bucket",
            Version = version,
            Name = name,
            Uuid = Str(root, "uuid"),
            ClusterUuid = Str(root, "clusterUUID"),
            TopKeys = root.EnumerateObject().Select(p => p.Name).ToList(),
            MapKeys = mapKeys,
            NodesExt = nodesExt,
            Nodes = nodes,
            ServerList = serverList,
            Map = map,
            Forward = forward,
            Flat = ConfigJson.Flatten(root),
        };
    }

    /// <summary>
    /// The host part of host:port, also for bracketed IPv6 addresses.
    /// </summary>
    public static string HostOf(string hostPort)
    {
        if (hostPort.StartsWith('['))
        {
            var close = hostPort.IndexOf(']');
            return close > 0 ? hostPort[..(close + 1)] : hostPort;
        }

        var colon = hostPort.LastIndexOf(':');
        return colon > 0 && hostPort.IndexOf(':') == colon && hostPort[(colon + 1)..].All(char.IsDigit) ? hostPort[..colon] : hostPort;
    }

    private static ExtNode ReadExtNode(JsonElement node)
    {
        var other = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in node.EnumerateObject())
        {
            if (property.Name is not ("hostname" or "thisNode" or "services" or "alternateAddresses"))
            {
                other[property.Name] = property.Value.GetRawText();
            }
        }

        return new ExtNode
        {
            Host = Str(node, "hostname"),
            Uuid = Str(node, "nodeUUID"),
            ThisNode = node.TryGetProperty("thisNode", out var thisNode) && thisNode.ValueKind == JsonValueKind.True,
            ServerGroup = Str(node, "serverGroup"),
            Keys = node.EnumerateObject().Select(p => p.Name).ToList(),
            Services = Ports(node, "services"),
            ServicesRaw = node.TryGetProperty("services", out var services) ? services.GetRawText() : "",
            Alternates = Alternates(node),
            AlternatesRaw = node.TryGetProperty("alternateAddresses", out var alternates) ? alternates.GetRawText() : "",
            Other = other,
        };
    }

    private static LegacyNode ReadLegacyNode(JsonElement node)
    {
        var other = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in node.EnumerateObject())
        {
            if (property.Name != "hostname") other[property.Name] = property.Value.GetRawText();
        }

        return new LegacyNode
        {
            Host = HostOf(Str(node, "hostname") ?? ""),
            Keys = node.EnumerateObject().Select(p => p.Name).ToList(),
            Alternates = Alternates(node),
            Other = other,
        };
    }

    private static List<AlternateAddress> Alternates(JsonElement node)
    {
        var result = new List<AlternateAddress>();
        if (!node.TryGetProperty("alternateAddresses", out var alternates) || alternates.ValueKind != JsonValueKind.Object) return result;
        foreach (var network in alternates.EnumerateObject())
        {
            result.Add(new AlternateAddress(network.Name, Str(network.Value, "hostname"), Ports(network.Value, "ports")));
        }

        return result;
    }

    private static List<PortEntry> Ports(JsonElement parent, string property)
    {
        var result = new List<PortEntry>();
        if (!parent.TryGetProperty(property, out var ports) || ports.ValueKind != JsonValueKind.Object) return result;
        foreach (var port in ports.EnumerateObject())
        {
            if (port.Value.ValueKind == JsonValueKind.Number && port.Value.TryGetInt64(out var value))
            {
                result.Add(new PortEntry(port.Name, value));
            }
        }

        return result;
    }

    /// <summary>
    /// Each vbucket as the hosts of its copies, active first. A missing copy (-1) is an empty string.
    /// Hosts instead of indexes keep a reordered server list from counting as moves.
    /// </summary>
    private static string[][]? Chains(JsonElement vbsm, string property, List<string> hosts)
    {
        if (!vbsm.TryGetProperty(property, out var map) || map.ValueKind != JsonValueKind.Array) return null;
        var result = new string[map.GetArrayLength()][];
        var i = 0;
        foreach (var chain in map.EnumerateArray())
        {
            result[i++] = chain.EnumerateArray()
                .Select(c => c.TryGetInt32(out var index) && index >= 0 && index < hosts.Count ? hosts[index] : "")
                .ToArray();
        }

        return result;
    }

    private static IEnumerable<JsonElement> Array(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().ToList()
            : [];

    private static string? Str(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static ulong? U64(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out var n) ? n : null;
}
