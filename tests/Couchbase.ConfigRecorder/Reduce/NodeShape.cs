using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Couchbase.ConfigRecorder.Reduce;

/// <summary>
/// What a node looks like in nodesExt and nodes[], and how each node changes over its life.
/// A member is one node incarnation, so a host that leaves and joins again with a new nodeUUID is two members.
/// </summary>
internal sealed partial class NodeShape
{
    private readonly KeyOrder _extKeys = new();
    private readonly KeyOrder _legacyKeys = new();
    private readonly KeyOrder _serviceKeys = new();
    private readonly Dictionary<string, SortedSet<long>> _portValues = new(StringComparer.Ordinal);
    private readonly List<string> _serviceSets = [];
    private readonly List<Member> _members = [];
    private readonly Dictionary<(string, string), Member> _byKey = new();
    private readonly Dictionary<string, KeyOrder> _alternatePortKeys = new(StringComparer.Ordinal);
    private readonly List<string> _alternateNetworks = [];
    private readonly Dictionary<string, Dictionary<string, string?>> _legacyAlternateHosts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, KeyOrder> _legacyAlternatePortKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _alternatesNotFollowingServices = new(StringComparer.Ordinal);
    private readonly Dictionary<int, int> _runLastSequence = new();
    private int _sequence;

    private sealed class Member
    {
        public int Index { get; init; }
        public string? Host { get; init; }
        public string? Uuid { get; init; }
        public string? ServerGroup { get; set; }
        public bool ServerGroupChanged { get; set; }
        public List<int> ServiceSets { get; } = [];
        public SortedSet<int> Runs { get; } = [];
        public bool Joined { get; init; }
        public bool Left { get; set; }
        public MapVersion First { get; init; }
        public MapVersion Last { get; set; }
        public int LastRun { get; set; }
        public int LastSequence { get; set; }
        public Dictionary<string, (string? Host, Dictionary<string, long> Ports)> Alternates { get; } = new(StringComparer.Ordinal);
        public bool AlternatesChanged { get; set; }
    }

    public int MemberCount => _members.Count;

    /// <summary>
    /// Collects ports, key orders and alternate addresses. Call it for every distinct body.
    /// </summary>
    public void Observe(ConfigBody body)
    {
        foreach (var node in body.NodesExt)
        {
            _extKeys.Merge(node.Keys);
            _serviceKeys.Merge(node.Services.Select(s => s.Key));
            foreach (var service in node.Services)
            {
                if (!_portValues.TryGetValue(service.Key, out var values))
                {
                    values = [];
                    _portValues[service.Key] = values;
                }

                values.Add(service.Port);
            }

            if (!_serviceSets.Contains(node.ServiceSet)) _serviceSets.Add(node.ServiceSet);
            foreach (var alternate in node.Alternates)
            {
                if (!_alternateNetworks.Contains(alternate.Network)) _alternateNetworks.Add(alternate.Network);
                PortKeys(_alternatePortKeys, alternate.Network).Merge(alternate.Ports.Select(p => p.Key));
            }

            foreach (var network in _alternateNetworks)
            {
                var alternate = node.Alternates.FirstOrDefault(a => a.Network == network);
                if (alternate is null) continue;
                if (_alternatePortKeys[network].Keys.Any(key => alternate.Ports.Any(p => p.Key == key) != (node.Port(key) is not null)))
                {
                    _alternatesNotFollowingServices.Add(network);
                }
            }
        }

        foreach (var node in body.Nodes)
        {
            _legacyKeys.Merge(node.Keys);
            foreach (var alternate in node.Alternates)
            {
                if (!_legacyAlternateHosts.TryGetValue(alternate.Network, out var hosts))
                {
                    hosts = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    _legacyAlternateHosts[alternate.Network] = hosts;
                }

                hosts.TryAdd(node.Host, alternate.Host);
                PortKeys(_legacyAlternatePortKeys, alternate.Network).Merge(alternate.Ports.Select(p => p.Key));
            }
        }
    }

    /// <summary>
    /// Follows each member through the versions. Call it for the first body of each version, in version order.
    /// </summary>
    public void ObserveVersion(int run, bool firstOfRun, ConfigBody body)
    {
        _sequence++;
        _runLastSequence[run] = _sequence;
        foreach (var node in body.NodesExt)
        {
            var key = (node.Host ?? "", node.Uuid ?? "");
            if (!_byKey.TryGetValue(key, out var member))
            {
                member = new Member
                {
                    Index = _members.Count,
                    Host = node.Host,
                    Uuid = node.Uuid,
                    ServerGroup = node.ServerGroup,
                    Joined = !firstOfRun,
                    First = body.Version,
                };
                _members.Add(member);
                _byKey[key] = member;
            }

            member.Runs.Add(run);
            member.Last = body.Version;
            member.LastRun = run;
            member.LastSequence = _sequence;
            if (member.ServerGroup != node.ServerGroup) member.ServerGroupChanged = true;
            var set = _serviceSets.IndexOf(node.ServiceSet);
            if (member.ServiceSets.Count == 0 || member.ServiceSets[^1] != set) member.ServiceSets.Add(set);
            foreach (var alternate in node.Alternates)
            {
                if (!member.Alternates.TryGetValue(alternate.Network, out var known))
                {
                    known = (alternate.Host, new Dictionary<string, long>(StringComparer.Ordinal));
                    member.Alternates[alternate.Network] = known;
                }

                if (known.Host != alternate.Host) member.AlternatesChanged = true;
                foreach (var port in alternate.Ports)
                {
                    if (known.Ports.TryGetValue(port.Key, out var value) && value != port.Port) member.AlternatesChanged = true;
                    known.Ports.TryAdd(port.Key, port.Port);
                }
            }
        }
    }

    /// <summary>
    /// Marks the members that were gone before the last version of a run.
    /// </summary>
    public void EndRun(int run)
    {
        if (!_runLastSequence.TryGetValue(run, out var last)) return;
        foreach (var member in _members.Where(m => m.LastRun == run && m.LastSequence < last))
        {
            member.Left = true;
        }
    }

    public JsonObject ToJson(Anonymizer anonymizer)
    {
        var shape = new JsonObject
        {
            ["nodesExtKeyOrder"] = Strings(_extKeys.Keys),
            ["ports"] = PortsJson(),
            ["serviceSets"] = ServiceSetsJson(),
            ["lifecycles"] = LifecyclesJson(),
            ["members"] = MembersJson(anonymizer),
            ["serverGroups"] = ServerGroupsJson(),
            ["alternateAddresses"] = AlternatesJson(anonymizer),
        };
        if (_legacyKeys.Keys.Count > 0)
        {
            shape["nodes"] = new JsonObject
            {
                ["keyOrder"] = Strings(_legacyKeys.Keys),
                ["alternateAddresses"] = LegacyAlternatesJson(anonymizer),
            };
        }

        return shape;
    }

    private JsonObject PortsJson()
    {
        var ports = new JsonObject();
        foreach (var key in _serviceKeys.Keys)
        {
            var values = _portValues[key];
            ports[key] = values.Count == 1 ? values.Min : new JsonArray(values.Select(v => (JsonNode)v).ToArray());
        }

        return ports;
    }

    private JsonArray ServiceSetsJson()
    {
        var sets = new JsonArray();
        for (var i = 0; i < _serviceSets.Count; i++)
        {
            var keys = _serviceSets[i].Length == 0 ? [] : _serviceSets[i].Split(',');
            sets.Add(new JsonObject
            {
                ["id"] = i,
                ["services"] = Strings(keys.Select(ServiceOf).Distinct()),
                ["keys"] = Strings(keys),
            });
        }

        return sets;
    }

    /// <summary>
    /// The service name of a port key, for example kvSSL is kv and indexHttps is index.
    /// </summary>
    public static string ServiceOf(string key) => ServicePrefix().Match(key) is { Success: true } m ? m.Value : key;

    private JsonArray LifecyclesJson()
    {
        var lifecycles = new JsonArray();
        foreach (var group in _members.GroupBy(m => (Sets: string.Join(",", m.ServiceSets), m.Joined, m.Left))
                     .OrderBy(g => g.Min(m => m.Index)))
        {
            var first = group.First();
            lifecycles.Add(new JsonObject
            {
                ["serviceSets"] = new JsonArray(first.ServiceSets.Select(s => (JsonNode)s).ToArray()),
                ["joined"] = first.Joined,
                ["left"] = first.Left,
                ["members"] = new JsonArray(group.Select(m => (JsonNode)m.Index).ToArray()),
            });
        }

        return lifecycles;
    }

    private JsonArray MembersJson(Anonymizer anonymizer)
    {
        var members = new JsonArray();
        foreach (var m in _members)
        {
            var member = new JsonObject
            {
                ["index"] = m.Index,
                ["hostname"] = m.Host is null ? null : anonymizer.Host(m.Host),
                ["nodeUUID"] = m.Uuid is null ? null : anonymizer.Uuid(m.Uuid),
                ["serverGroup"] = m.ServerGroup,
                ["serviceSets"] = new JsonArray(m.ServiceSets.Select(s => (JsonNode)s).ToArray()),
                ["joined"] = m.Joined,
                ["left"] = m.Left,
                ["runs"] = new JsonArray(m.Runs.Select(r => (JsonNode)r).ToArray()),
                ["firstRev"] = m.First.Rev,
                ["lastRev"] = m.Last.Rev,
            };
            if (m.ServerGroupChanged) member["serverGroupChanged"] = true;
            if (m.AlternatesChanged) member["alternateAddressesChanged"] = true;
            members.Add(member);
        }

        return members;
    }

    private JsonObject ServerGroupsJson()
    {
        var groups = _members.Select(m => m.ServerGroup).ToList();
        var distinct = groups.OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var prefixes = distinct.Select(g => NumberSuffix().Match(g)).ToList();
        string? pattern = null;
        if (prefixes.Count > 0 && prefixes.All(m => m.Success) && prefixes.Select(m => m.Groups[1].Value).Distinct().Count() == 1)
        {
            pattern = prefixes[0].Groups[1].Value + "{n}";
        }

        return new JsonObject
        {
            ["pattern"] = pattern,
            ["distinct"] = Strings(distinct),
            ["perMember"] = new JsonArray(groups.Select(g => (JsonNode?)g).ToArray()),
        };
    }

    private JsonObject AlternatesJson(Anonymizer anonymizer)
    {
        var result = new JsonObject();
        foreach (var network in _alternateNetworks)
        {
            var withNetwork = _members.Where(m => m.Alternates.ContainsKey(network)).ToList();
            var hosts = withNetwork.Select(m => m.Alternates[network].Host is { } h ? anonymizer.Host(h) : null).ToList();
            var distinctHosts = hosts.Distinct().ToList();
            var entry = new JsonObject
            {
                ["members"] = withNetwork.Count,
                ["hostname"] = distinctHosts.Count == 1 && withNetwork.Count > 1 ? "shared" : "perNode",
            };

            // True when a node lists an alternate port exactly when its services list the same key.
            entry["portsFollowServices"] = !_alternatesNotFollowingServices.Contains(network);
            if (distinctHosts.Count == 1 && withNetwork.Count > 1) entry["sharedHostname"] = distinctHosts[0];
            else entry["perNode"] = new JsonArray(_members.Select(m => (JsonNode?)(m.Alternates.TryGetValue(network, out var a) && a.Host is { } h ? anonymizer.Host(h) : null)).ToArray());

            entry["keyOrder"] = Strings(_alternatePortKeys[network].Keys);
            var ports = new JsonObject();
            foreach (var key in _alternatePortKeys[network].Keys)
            {
                var values = _members.Select(m => m.Alternates.TryGetValue(network, out var a) && a.Ports.TryGetValue(key, out var p) ? p : (long?)null).ToList();
                var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
                if (present.Distinct().Count() == 1 && present.Count == withNetwork.Count)
                {
                    ports[key] = new JsonObject { ["shared"] = present[0] };
                    continue;
                }

                var port = new JsonObject { ["perNode"] = new JsonArray(values.Select(v => (JsonNode?)v).ToArray()) };
                var sorted = present.Distinct().Order().ToList();
                if (sorted.Count == present.Count && sorted.Count > 1 && sorted[^1] - sorted[0] == sorted.Count - 1)
                {
                    port["contiguousFrom"] = sorted[0];
                }

                ports[key] = port;
            }

            entry["ports"] = ports;
            result[network] = entry;
        }

        return result;
    }

    private JsonObject LegacyAlternatesJson(Anonymizer anonymizer)
    {
        var result = new JsonObject();
        foreach (var (network, hosts) in _legacyAlternateHosts.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var distinct = hosts.Values.Select(h => h is null ? null : anonymizer.Host(h)).Distinct().ToList();
            var entry = new JsonObject { ["hostname"] = distinct.Count == 1 && hosts.Count > 1 ? "shared" : "perNode" };
            if (distinct.Count == 1 && hosts.Count > 1) entry["sharedHostname"] = distinct[0];
            entry["portKeys"] = Strings(_legacyAlternatePortKeys[network].Keys);
            result[network] = entry;
        }

        return result;
    }

    private static KeyOrder PortKeys(Dictionary<string, KeyOrder> orders, string network)
    {
        if (!orders.TryGetValue(network, out var order))
        {
            order = new KeyOrder();
            orders[network] = order;
        }

        return order;
    }

    public static JsonArray Strings(IEnumerable<string> values) => new(values.Select(v => (JsonNode)v).ToArray());

    [GeneratedRegex("^[a-z0-9]+")]
    private static partial Regex ServicePrefix();

    [GeneratedRegex(@"^(.*?)(\d+)$")]
    private static partial Regex NumberSuffix();
}
