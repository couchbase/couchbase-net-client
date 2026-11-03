using System.Text.Json.Nodes;

namespace Couchbase.ConfigRecorder.Reduce;

/// <summary>
/// Compares the first body of each version with the first body of the version before it, per run and kind.
/// thisNode is left out of the comparison, because it depends on the node that sent the body.
/// </summary>
internal sealed class Transitions
{
    /// <summary>
    /// Step labels in the order they are written.
    /// </summary>
    public static readonly string[] Labels =
    [
        "nodeAddedToNodesExt", "nodeRemovedFromNodesExt", "nodeAddedToNodes", "nodeRemovedFromNodes",
        "nodesExtOrderChanged", "nodesOrderChanged", "servicesChanged", "alternateAddressesChanged",
        "serverGroupChanged", "nodeUuidChanged", "serverListChanged", "vbucketMapChanged",
        "forwardMapSet", "forwardMapChanged", "forwardMapCleared", "revEpochChanged", "otherFieldsChanged", "revOnly",
    ];

    private static readonly HashSet<string> Milestones = new(Labels.Except(["vbucketMapChanged", "forwardMapChanged", "revOnly"]));

    private static readonly string[] ComparedElsewhere =
    [
        "rev", "nodesExt", "nodes", "vBucketServerMap.serverList", "vBucketServerMap.vBucketMap", "vBucketServerMap.vBucketMapForward",
    ];

    private readonly Dictionary<(int Run, string Kind), ConfigBody> _previous = new();
    private readonly Dictionary<(int Run, string Kind), Series> _series = new();

    public sealed record Step(int Run, MapVersion From, MapVersion To, double? GapMs, IReadOnlyList<string> Labels,
        int Moved, int ActiveMoved, IReadOnlyList<string> Other);

    private sealed class Series
    {
        public List<(MapVersion Version, double? T, bool Forward)> Versions { get; } = [];
        public List<Step> Steps { get; } = [];
    }

    /// <summary>
    /// Adds the first body of a version. <paramref name="t"/> is the first arrival of the version from any source.
    /// </summary>
    public void Add(int run, ConfigBody body, double? t)
    {
        var key = (run, body.Kind);
        if (!_series.TryGetValue(key, out var series))
        {
            series = new Series();
            _series[key] = series;
        }

        if (_previous.TryGetValue(key, out var previous))
        {
            var previousT = series.Versions[^1].T;
            series.Steps.Add(Compare(run, previous, body, t - previousT));
        }

        series.Versions.Add((body.Version, t, body.HasForward));
        _previous[key] = body;
    }

    public IEnumerable<Step> Steps(string kind) =>
        _series.Where(p => p.Key.Kind == kind).OrderBy(p => p.Key.Run).SelectMany(p => p.Value.Steps);

    public int Versions(string kind) => _series.Where(p => p.Key.Kind == kind).Sum(p => p.Value.Versions.Count);

    private static Step Compare(int run, ConfigBody before, ConfigBody after, double? gapMs)
    {
        var labels = new HashSet<string>(StringComparer.Ordinal);
        var other = new SortedSet<string>(StringComparer.Ordinal);

        CompareNodesExt(before, after, labels, other);
        CompareLegacyNodes(before, after, labels, other);
        if (!before.ServerList.SequenceEqual(after.ServerList, StringComparer.Ordinal)) labels.Add("serverListChanged");

        var (moved, activeMoved) = Moves(before.Map, after.Map);
        if (moved > 0) labels.Add("vbucketMapChanged");
        switch (before.Forward, after.Forward)
        {
            case (null, not null):
                labels.Add("forwardMapSet");
                break;
            case (not null, null):
                labels.Add("forwardMapCleared");
                break;
            case (not null, not null) when Moves(before.Forward, after.Forward).Moved > 0:
                labels.Add("forwardMapChanged");
                break;
        }

        foreach (var path in before.Flat.Keys.Union(after.Flat.Keys).Where(p => !IsComparedElsewhere(p)))
        {
            before.Flat.TryGetValue(path, out var oldValue);
            after.Flat.TryGetValue(path, out var newValue);
            if (oldValue == newValue) continue;
            if (path == "revEpoch")
            {
                labels.Add("revEpochChanged");
                continue;
            }

            labels.Add("otherFieldsChanged");
            other.Add(ConfigJson.Normalize(path));
        }

        if (labels.Count == 0) labels.Add("revOnly");
        return new Step(run, before.Version, after.Version, gapMs is null ? null : Math.Round(gapMs.Value),
            Labels.Where(labels.Contains).ToList(), moved, activeMoved, other.ToList());
    }

    private static void CompareNodesExt(ConfigBody before, ConfigBody after, HashSet<string> labels, SortedSet<string> other)
    {
        var oldHosts = before.NodesExt.Select(n => n.Host ?? "").ToList();
        var newHosts = after.NodesExt.Select(n => n.Host ?? "").ToList();
        if (newHosts.Except(oldHosts).Any()) labels.Add("nodeAddedToNodesExt");
        if (oldHosts.Except(newHosts).Any()) labels.Add("nodeRemovedFromNodesExt");
        var common = oldHosts.Intersect(newHosts).ToList();
        if (!oldHosts.Where(common.Contains).SequenceEqual(newHosts.Where(common.Contains))) labels.Add("nodesExtOrderChanged");

        foreach (var host in common)
        {
            var a = before.NodesExt.First(n => (n.Host ?? "") == host);
            var b = after.NodesExt.First(n => (n.Host ?? "") == host);
            if (a.ServicesRaw != b.ServicesRaw) labels.Add("servicesChanged");
            if (a.AlternatesRaw != b.AlternatesRaw) labels.Add("alternateAddressesChanged");
            foreach (var property in a.Other.Keys.Union(b.Other.Keys))
            {
                if (a.Other.GetValueOrDefault(property) == b.Other.GetValueOrDefault(property)) continue;
                switch (property)
                {
                    case "serverGroup":
                        labels.Add("serverGroupChanged");
                        break;
                    case "nodeUUID":
                        labels.Add("nodeUuidChanged");
                        break;
                    default:
                        labels.Add("otherFieldsChanged");
                        other.Add($"nodesExt[].{property}");
                        break;
                }
            }
        }
    }

    private static void CompareLegacyNodes(ConfigBody before, ConfigBody after, HashSet<string> labels, SortedSet<string> other)
    {
        var oldHosts = before.Nodes.Select(n => n.Host).ToList();
        var newHosts = after.Nodes.Select(n => n.Host).ToList();
        if (newHosts.Except(oldHosts).Any()) labels.Add("nodeAddedToNodes");
        if (oldHosts.Except(newHosts).Any()) labels.Add("nodeRemovedFromNodes");
        var common = oldHosts.Intersect(newHosts).ToList();
        if (!oldHosts.Where(common.Contains).SequenceEqual(newHosts.Where(common.Contains))) labels.Add("nodesOrderChanged");

        foreach (var host in common)
        {
            var a = before.Nodes.First(n => n.Host == host);
            var b = after.Nodes.First(n => n.Host == host);
            foreach (var property in a.Other.Keys.Union(b.Other.Keys))
            {
                if (a.Other.GetValueOrDefault(property) == b.Other.GetValueOrDefault(property)) continue;
                labels.Add("otherFieldsChanged");
                other.Add($"nodes[].{property}");
            }
        }
    }

    /// <summary>
    /// The vbuckets whose copies sit on other hosts, and the vbuckets whose active copy moved.
    /// A map that appears or goes away counts every vbucket.
    /// </summary>
    private static (int Moved, int ActiveMoved) Moves(string[][]? before, string[][]? after)
    {
        if (before is null && after is null) return (0, 0);
        if (before is null || after is null || before.Length != after.Length)
        {
            var count = Math.Max(before?.Length ?? 0, after?.Length ?? 0);
            return (count, count);
        }

        int moved = 0, active = 0;
        for (var i = 0; i < before.Length; i++)
        {
            if (!before[i].SequenceEqual(after[i], StringComparer.Ordinal)) moved++;
            if (before[i].FirstOrDefault() != after[i].FirstOrDefault()) active++;
        }

        return (moved, active);
    }

    private static bool IsComparedElsewhere(string path) =>
        path.EndsWith(".thisNode", StringComparison.Ordinal)
        || ComparedElsewhere.Any(p => path == p || path.StartsWith(p + "[", StringComparison.Ordinal) || path.StartsWith(p + ".", StringComparison.Ordinal));

    public JsonObject ToJson(string kind, IReadOnlyList<(int Run, List<(MapVersion Version, double T)> Seen)> seen,
        JsonArray missingThisNode)
    {
        var steps = Steps(kind).ToList();
        var result = new JsonObject
        {
            ["versionsWithBody"] = Versions(kind),
            ["versionsSeen"] = seen.Sum(s => s.Seen.Count),
            ["steps"] = steps.Count,
            ["labels"] = LabelCounts(steps),
            ["combinations"] = Combinations(steps),
            ["otherPaths"] = OtherPaths(steps),
            ["stepGapMs"] = Distribution(steps.Where(s => s.GapMs is not null).Select(s => s.GapMs!.Value)),
            ["versionGapMs"] = Distribution(seen.SelectMany(s => s.Seen.Zip(s.Seen.Skip(1), (a, b) => b.T - a.T))),
        };
        if (kind == "bucket")
        {
            var moving = steps.Where(s => s.Moved > 0).ToList();
            result["vbucketsMovedPerStep"] = Distribution(moving.Select(s => (double)s.Moved), total: true);
            result["activeMovedPerStep"] = Distribution(moving.Select(s => (double)s.ActiveMoved), total: true);
            result["rebalances"] = Rebalances(seen);
        }

        result["missingThisNode"] = missingThisNode;
        var list = new JsonArray();
        foreach (var step in steps)
        {
            list.Add(StepJson(step));
        }

        result["list"] = list;
        return result;
    }

    private static JsonObject StepJson(Step step)
    {
        var json = new JsonObject
        {
            ["run"] = step.Run,
            ["from"] = step.From.Rev,
            ["to"] = step.To.Rev,
        };
        if (step.From.Epoch != step.To.Epoch)
        {
            json["fromEpoch"] = step.From.Epoch;
            json["toEpoch"] = step.To.Epoch;
        }

        json["gapMs"] = step.GapMs;
        json["labels"] = NodeShape.Strings(step.Labels);
        if (step.Moved > 0)
        {
            json["moved"] = step.Moved;
            json["activeMoved"] = step.ActiveMoved;
        }

        if (step.Other.Count > 0) json["other"] = NodeShape.Strings(step.Other);
        return json;
    }

    private static JsonObject LabelCounts(List<Step> steps)
    {
        var counts = new JsonObject();
        foreach (var label in Labels)
        {
            var count = steps.Count(s => s.Labels.Contains(label));
            if (count > 0) counts[label] = count;
        }

        return counts;
    }

    private static JsonArray Combinations(List<Step> steps)
    {
        var result = new JsonArray();
        foreach (var group in steps.GroupBy(s => string.Join("+", s.Labels))
                     .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            result.Add(new JsonObject { ["labels"] = NodeShape.Strings(group.First().Labels), ["steps"] = group.Count() });
        }

        return result;
    }

    private static JsonObject OtherPaths(List<Step> steps)
    {
        var result = new JsonObject();
        foreach (var group in steps.SelectMany(s => s.Other).GroupBy(p => p).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            result[group.Key] = group.Count();
        }

        return result;
    }

    /// <summary>
    /// Median, p95 and max with the same rank rule as the summarize report.
    /// </summary>
    public static JsonObject Distribution(IEnumerable<double> values, bool total = false)
    {
        var sorted = values.Order().ToList();
        var result = new JsonObject { ["count"] = sorted.Count };
        if (sorted.Count == 0) return result;
        double At(double q) => sorted[Math.Min(sorted.Count - 1, (int)(q * sorted.Count))];
        result["median"] = Math.Round(At(0.5));
        result["p95"] = Math.Round(At(0.95));
        result["max"] = Math.Round(sorted[^1]);
        if (total) result["total"] = Math.Round(sorted.Sum());
        return result;
    }

    /// <summary>
    /// A rebalance is a run of bucket versions with a forward map. It also takes the node additions
    /// since the previous rebalance before it, and the node removals until the next rebalance after it.
    /// The server adds nodes to the bucket before it moves vbuckets, and removes them after.
    /// </summary>
    private JsonArray Rebalances(IReadOnlyList<(int Run, List<(MapVersion Version, double T)> Seen)> seen)
    {
        var result = new JsonArray();
        foreach (var ((run, _), series) in _series.Where(p => p.Key.Kind == "bucket").OrderBy(p => p.Key.Run))
        {
            var versions = series.Versions;

            // Step i goes from version i - 1 to version i.
            HashSet<string> LabelsAt(int i) => i <= 0 ? [] : series.Steps[i - 1].Labels.ToHashSet(StringComparer.Ordinal);

            var episodes = new List<(int Start, int? End)>();
            for (var i = 0; i < versions.Count; i++)
            {
                if (!versions[i].Forward || (i > 0 && versions[i - 1].Forward)) continue;
                var end = Enumerable.Range(i + 1, versions.Count - i - 1).Where(j => !versions[j].Forward).Cast<int?>().FirstOrDefault();
                episodes.Add((i, end));
            }

            var lowerBound = 0;
            for (var e = 0; e < episodes.Count; e++)
            {
                var (start, end) = episodes[e];
                var from = Enumerable.Range(lowerBound + 1, Math.Max(0, start - lowerBound))
                    .Where(i => LabelsAt(i).Overlaps(["nodeAddedToNodesExt", "nodeAddedToNodes"]))
                    .DefaultIfEmpty(start).Min();
                int? to = end;
                if (end is { } endIndex)
                {
                    var upperBound = e + 1 < episodes.Count ? episodes[e + 1].Start : versions.Count;
                    to = Enumerable.Range(endIndex, Math.Max(0, upperBound - endIndex))
                        .Where(i => LabelsAt(i).Overlaps(["nodeRemovedFromNodesExt", "nodeRemovedFromNodes"]))
                        .DefaultIfEmpty(endIndex).Max();
                    lowerBound = to.Value;
                }

                var last = to ?? versions.Count - 1;
                var startT = versions[from].T;
                var firstStep = Math.Max(from, 1);
                var window = Enumerable.Range(firstStep, Math.Max(0, last - firstStep + 1)).Select(i => series.Steps[i - 1]).ToList();
                var seenInRun = seen.Where(s => s.Run == run).SelectMany(s => s.Seen).ToList();
                var milestones = new JsonArray();
                for (var i = firstStep; i <= last; i++)
                {
                    var labels = series.Steps[i - 1].Labels;
                    if (!labels.Any(Milestones.Contains)) continue;
                    milestones.Add(new JsonObject
                    {
                        ["rev"] = versions[i].Version.Rev,
                        ["atSeconds"] = Seconds(versions[i].T - startT),
                        ["labels"] = NodeShape.Strings(labels),
                    });
                }

                result.Add(new JsonObject
                {
                    ["run"] = run,
                    ["fromRev"] = versions[from].Version.Rev,
                    ["toRev"] = to is null ? null : versions[to.Value].Version.Rev,
                    ["finished"] = to is not null,
                    ["versionsWithBody"] = last - from + 1,
                    ["versionsSeen"] = seenInRun.Count(v => v.Version.CompareTo(versions[from].Version) >= 0
                                                            && v.Version.CompareTo(versions[last].Version) <= 0),
                    ["forwardMapVersions"] = (end ?? versions.Count) - start,
                    ["seconds"] = Seconds(versions[last].T - startT),
                    ["vbucketsMoved"] = window.Sum(s => s.Moved),
                    ["activeMoved"] = window.Sum(s => s.ActiveMoved),
                    ["milestones"] = milestones,
                });
            }
        }

        return result;
    }

    private static double? Seconds(double? ms) => ms is null ? null : Math.Round(ms.Value / 1000, 1);
}
