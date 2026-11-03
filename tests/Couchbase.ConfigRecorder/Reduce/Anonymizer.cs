using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Couchbase.ConfigRecorder.Reduce;

/// <summary>
/// Replaces host names and UUIDs with fake values. The fakes depend only on the order of first appearance,
/// so every file of a profile uses the same fakes.
/// </summary>
internal sealed partial class Anonymizer
{
    public const string SharedAlternateHost = "private-endpoint.example.com";

    private readonly Dictionary<string, string> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _uuids = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _otherHosts = new(StringComparer.OrdinalIgnoreCase);
    private List<KeyValuePair<string, string>>? _replacements;

    public int NodeCount { get; private set; }

    public IEnumerable<string> OriginalHosts => _hosts.Keys.Concat(_otherHosts);

    public IEnumerable<string> OriginalUuids => _uuids.Keys;

    /// <summary>
    /// Maps a node host name to node-N.example.com. N counts from 0 in order of first appearance.
    /// </summary>
    public void AddNodeHost(string host)
    {
        if (_hosts.ContainsKey(host)) return;
        _hosts[host] = $"node-{NodeCount++}.example.com";
        _replacements = null;
    }

    /// <summary>
    /// Maps an alternate host name. A name that several nodes share is the Private Link endpoint.
    /// A name of one node gets the fake name of that node under the network name.
    /// </summary>
    public void AddAlternateHost(string host, string network, IReadOnlyCollection<string> nodeHosts)
    {
        if (_hosts.ContainsKey(host)) return;
        if (nodeHosts.Count == 1)
        {
            _hosts[host] = Host(nodeHosts.First()).Replace(".example.com", $".{network}.example.com", StringComparison.Ordinal);
        }
        else
        {
            var shared = _hosts.Values.Count(v => v.StartsWith("private-endpoint", StringComparison.Ordinal));
            _hosts[host] = shared == 0 ? SharedAlternateHost : $"private-endpoint-{shared + 1}.example.com";
        }

        _replacements = null;
    }

    /// <summary>
    /// A host name that no body holds, such as the seed host of the connection string. It is only scanned for.
    /// </summary>
    public void AddOtherHost(string host)
    {
        if (!_hosts.ContainsKey(host)) _otherHosts.Add(host);
    }

    /// <summary>
    /// Maps a UUID to a fake hex string of the same length. The role letter shows what it was.
    /// </summary>
    public void AddUuid(string uuid, char role)
    {
        if (string.IsNullOrEmpty(uuid) || _uuids.ContainsKey(uuid)) return;
        var index = _uuids.Values.Count(v => v[0] == role);
        var suffix = index.ToString("x");
        _uuids[uuid] = uuid.Length <= suffix.Length + 1
            ? new string(role, uuid.Length)
            : role + new string('0', uuid.Length - 1 - suffix.Length) + suffix;
        _replacements = null;
    }

    public string Host(string host) => _hosts.TryGetValue(host, out var fake) ? fake : host;

    public string Uuid(string uuid) => _uuids.TryGetValue(uuid, out var fake) ? fake : uuid;

    /// <summary>
    /// Rewrites every known host name and UUID inside a string, for example inside a URI.
    /// </summary>
    public string Text(string value)
    {
        _replacements ??= _hosts.Concat(_uuids)
            .OrderByDescending(p => p.Key.Length)
            .ThenBy(p => p.Key, StringComparer.Ordinal)
            .ToList();
        foreach (var (original, fake) in _replacements)
        {
            if (value.Contains(original, StringComparison.OrdinalIgnoreCase))
            {
                value = value.Replace(original, fake, StringComparison.OrdinalIgnoreCase);
            }
        }

        return value;
    }

    /// <summary>
    /// Rewrites every string value of a JSON tree in place. Property names stay as they are.
    /// </summary>
    public void Rewrite(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var name in obj.Select(p => p.Key).ToList())
                {
                    if (obj[name] is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        obj[name] = Text(text);
                    }
                    else
                    {
                        Rewrite(obj[name]);
                    }
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i] is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        array[i] = Text(text);
                    }
                    else
                    {
                        Rewrite(array[i]);
                    }
                }

                break;
        }
    }

    /// <summary>
    /// Strings that must not appear in a profile. These are the original host names and UUIDs, the labels
    /// and dash separated parts of the DNS names, the first 6 characters of long labels, and the first two
    /// parts of IPv4 addresses. A part is left out when a fake name or a JSON key holds it, for example
    /// a domain part "project" and the key "projector".
    /// </summary>
    public List<string> Fragments(IReadOnlyCollection<string> keys)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "com", "net", "org", "local", "localhost" };
        foreach (var fake in _hosts.Values)
        {
            foreach (var part in fake.Split('.', '-'))
            {
                allowed.Add(part);
            }
        }

        var parts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fragments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var host in OriginalHosts)
        {
            fragments.Add(host);
            if (Ipv4().IsMatch(host))
            {
                var octets = host.Split('.');
                fragments.Add($"{octets[0]}.{octets[1]}.");
                continue;
            }

            var labels = host.Split('.');
            foreach (var label in labels.Take(labels.Length - 1))
            {
                var tokens = label.Split('-');
                if (label.Length >= 4 && !tokens.All(allowed.Contains)) parts.Add(label);
                if (label.Length >= 10 && !label.Contains('-') && label.Any(char.IsDigit)) parts.Add(label[..6]);
                foreach (var token in tokens)
                {
                    if (token.Length >= 5 && !allowed.Contains(token)) parts.Add(token);
                }
            }
        }

        fragments.UnionWith(parts.Where(p => !keys.Any(k => k.Contains(p, StringComparison.OrdinalIgnoreCase))));
        fragments.UnionWith(_uuids.Keys);
        return fragments.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3}){3}$")]
    private static partial Regex Ipv4();
}
