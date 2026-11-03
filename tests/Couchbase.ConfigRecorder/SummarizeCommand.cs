using System.Globalization;
using System.Text.Json;

namespace Couchbase.ConfigRecorder;

/// <summary>
/// Reads a run folder and prints a readable report.
/// </summary>
internal sealed class SummarizeCommand
{
    private const int MaxDiffLines = 40;
    private const string ProbeBody = RunFolder.ProbeBody;
    private const int MaxValueLength = 70;
    private static readonly string[] CollapsedPrefixes = ["vBucketServerMap.vBucketMap[", "vBucketServerMap.vBucketMapForward["];

    private readonly RunFolder _run;
    private readonly TextWriter _out;
    private readonly IReadOnlyList<RunEvent> _events;
    private readonly Dictionary<string, Dictionary<string, string>?> _flatBodies = new(StringComparer.Ordinal);

    private SummarizeCommand(RunFolder run, TextWriter output)
    {
        _run = run;
        _events = run.Events;
        _out = output;
    }

    public static int Run(string folder, TextWriter output)
    {
        if (!File.Exists(RunFolder.EventsPath(folder)))
        {
            Console.Error.WriteLine($"No events.jsonl in '{folder}'.");
            return 2;
        }

        var command = new SummarizeCommand(RunFolder.Load(folder), output);
        command.Print();
        return 0;
    }

    private void Print()
    {
        PrintOverview();
        PrintCounts();
        PrintConnections();
        foreach (var scope in new[] { "cluster", "bucket" })
        {
            PrintProbeTimeline(scope);
        }

        foreach (var scope in new[] { "cluster", "bucket" })
        {
            PrintArrivals(scope);
        }

        foreach (var scope in new[] { "cluster", "bucket" })
        {
            PrintDiffs(scope);
        }

        PrintPaths();
        PrintDns();
        PrintLoad();
        PrintErrors();
    }

    private void Heading(string title)
    {
        _out.WriteLine();
        _out.WriteLine(title);
        _out.WriteLine(new string('=', title.Length));
    }

    private static string S(double ms) => (ms / 1000).ToString("0.000", CultureInfo.InvariantCulture);

    private void PrintOverview()
    {
        Heading("Run");
        _out.WriteLine($"Folder      {Path.GetFullPath(_run.Folder)}");
        if (_events.Count == 0)
        {
            _out.WriteLine("No events.");
            return;
        }

        var first = _events[0];
        var last = _events[^1];
        _out.WriteLine($"Started     {first.Ts}");
        _out.WriteLine($"Length      {S(last.T - first.T)} s");
        _out.WriteLine($"Events      {_events.Count}{(_run.BadLines > 0 ? $" ({_run.BadLines} unreadable lines skipped)" : "")}");
        var start = _events.FirstOrDefault(e => e.Stream == "recorder" && e.Kind == "start");
        if (start is not null)
        {
            _out.WriteLine($"SDK         {start.Str("sdkVersion")}");
            if (start.E.TryGetProperty("settings", out var settings))
            {
                _out.WriteLine($"Connection  {settings.GetProperty("connectionString").GetString()}");
                _out.WriteLine($"Bucket      {_run.BucketName}");
            }
        }

        var stop = _events.LastOrDefault(e => e.Stream == "recorder" && e.Kind == "stop");
        _out.WriteLine($"Stopped     {(stop is null ? "no stop event, the run did not end cleanly" : stop.Str("reason"))}");
    }

    private void PrintCounts()
    {
        Heading("Events per stream and kind");
        foreach (var group in _events.GroupBy(e => (e.Stream, e.Kind)).OrderBy(g => g.Key.Stream, StringComparer.Ordinal)
                     .ThenBy(g => g.Key.Kind, StringComparer.Ordinal))
        {
            _out.WriteLine($"{group.Key.Stream,-10} {group.Key.Kind,-16} {group.Count(),8}");
        }
    }

    private void PrintConnections()
    {
        Heading("Connections");
        foreach (var stream in new[] { "tap", "probe-tap" })
        {
            var connects = _events.Where(e => e.Stream == stream && e.Kind == "connect").ToList();
            var closes = _events.Count(e => e.Stream == stream && e.Kind == "close");
            _out.WriteLine($"{stream,-10} opened {connects.Count}, closed {closes}, hosts {string.Join(", ", connects.Select(c => $"{c.Str("host")}:{c.U64("port")}").Distinct())}");
        }

        foreach (var stream in new[] { "tap", "probe-tap" })
        {
            var granted = _events.Where(e => e.Stream == stream && e.Kind == "hello")
                .Select(e => e.E.TryGetProperty("granted", out var g)
                    ? string.Join(" ", g.EnumerateArray().Select(x => x.GetString()).OrderBy(x => x, StringComparer.Ordinal))
                    : "")
                .Distinct()
                .ToList();
            foreach (var set in granted)
            {
                _out.WriteLine($"{stream,-10} HELLO granted {set}");
            }
        }

        foreach (var e in _events.Where(e => e.Kind is "tap-stopped" or "frame-error"))
        {
            _out.WriteLine($"{S(e.T),10} {e.Stream} {e.Kind} {e.Str("host")}:{e.U64("port")} cid {e.Str("cid")} {e.Str("detail")}");
        }
    }

    private void PrintProbeTimeline(string scope)
    {
        Heading($"Probe timeline, {scope} config (seconds since start, when each node first served each version)");
        var versions = _events.Where(e => e.Stream == "probe" && e.Kind == "version" && e.Str("scope") == scope).ToList();
        if (versions.Count == 0)
        {
            _out.WriteLine("No probe versions.");
            return;
        }

        var nodes = _events.Where(e => e.Stream == "probe" && e.Str("node") is not null)
            .Select(e => e.Str("node")!).Distinct().ToList();
        var labels = nodes.Select((n, i) => (n, $"n{i + 1}")).ToDictionary(p => p.n, p => p.Item2);
        foreach (var node in nodes)
        {
            _out.WriteLine($"{labels[node],-4} {node}");
        }

        var first = new Dictionary<(MapVersion, string), double>();
        foreach (var e in versions)
        {
            var key = (new MapVersion(e.U64("epoch") ?? 0, e.U64("rev") ?? 0), e.Str("node")!);
            first.TryAdd(key, e.T);
        }

        var all = first.Keys.Select(k => k.Item1).Distinct().OrderBy(v => v).ToList();
        _out.WriteLine();
        _out.WriteLine($"{"version",-14}" + string.Concat(nodes.Select(n => $"{labels[n],10}")));
        foreach (var v in all)
        {
            _out.WriteLine($"{v,-14}" + string.Concat(nodes.Select(n => first.TryGetValue((v, n), out var t) ? $"{S(t),10}" : $"{".",10}")));
        }

        _out.WriteLine();
        foreach (var e in _events.Where(e => e.Stream == "probe" && e.Kind is "node-added" or "node-removed" or "down" or "up"
                                                && (e.Str("scope") is null || e.Str("scope") == scope)))
        {
            var detail = e.Kind switch
            {
                "down" => $"{e.Str("error")}, {e.Str("detail")}",
                "up" => e.Bool("first") == true ? "first answer" : $"after {S(e.Dbl("downMs") ?? 0)} s",
                _ => e.Str("source"),
            };
            _out.WriteLine($"{S(e.T),10} {labels.GetValueOrDefault(e.Str("node") ?? "", "?"),-4} {e.Kind,-12} {detail}");
        }

        // Coverage. A node can miss a version that came and went between two probes, or that was
        // published before the node joined or after it left.
        var seenAnywhere = _run.AllVersions(scope);
        _out.WriteLine();
        foreach (var node in nodes)
        {
            var seen = first.Keys.Where(k => k.Item2 == node).Select(k => k.Item1).ToHashSet();
            var missing = seenAnywhere.Where(v => !seen.Contains(v)).ToList();
            _out.WriteLine($"{labels[node],-4} served {seen.Count} of {seenAnywhere.Count} versions seen anywhere" +
                           (missing.Count > 0 ? $", not seen on this node {string.Join(" ", missing.Take(10))}{(missing.Count > 10 ? $" and {missing.Count - 10} more" : "")}" : ""));
        }
    }

    private void PrintArrivals(string scope)
    {
        Heading($"New {scope} versions, first arrival per source (seconds since start)");
        var arrivals = _run.Arrivals(scope).Where(a => a.Source != ProbeBody).ToList();
        if (arrivals.Count == 0)
        {
            _out.WriteLine("No versions.");
            return;
        }

        var sources = new List<string> { "brief push", "full push", "fetch reply", "nmvb body", "probe", "dispatched" };
        sources.AddRange(new[] { "probe brief push", "probe full push" }.Where(s => arrivals.Any(a => a.Source == s)));
        var appliedColumns = scope == "bucket" ? new[] { "CurrentConfig", "KeyMapper" } : ["GlobalConfig"];

        var firstBy = arrivals.GroupBy(a => (a.Version, a.Source)).ToDictionary(g => g.Key, g => g.Min(a => a.T));
        var versions = arrivals.Select(a => a.Version).Distinct().OrderBy(v => v).ToList();
        var states = _events.Where(e => e.Stream == "sdk" && e.Kind == "state").ToList();

        var columns = sources.Concat(appliedColumns).ToList();
        var widths = columns.Select(c => Math.Max(12, c.Length + 2)).ToList();
        _out.WriteLine($"{"version",-14}" + string.Concat(columns.Select((c, i) => c.PadLeft(widths[i]))));
        foreach (var v in versions)
        {
            var cells = sources.Select(s => firstBy.TryGetValue((v, s), out var t) ? S(t) : "-").ToList();
            foreach (var column in appliedColumns)
            {
                var applied = states.FirstOrDefault(st => Applied(st, column, v));
                cells.Add(applied is null ? "-" : S(applied.T));
            }

            _out.WriteLine($"{v,-14}" + string.Concat(cells.Select((c, i) => c.PadLeft(widths[i]))));
        }

        _out.WriteLine();
        _out.WriteLine($"{versions.Count} versions from {versions[0]} to {versions[^1]}.");
        var earliest = versions.ToDictionary(v => v, v => arrivals.Where(a => a.Version == v && a.Source != "dispatched").Select(a => a.T).DefaultIfEmpty(double.NaN).Min());
        foreach (var source in sources)
        {
            var delivered = versions.Count(v => firstBy.ContainsKey((v, source)));
            if (delivered == 0) continue;
            var wasFirst = source == "dispatched" ? 0 : versions.Count(v => firstBy.TryGetValue((v, source), out var t) && t <= earliest[v]);
            _out.WriteLine($"  {source,-16} delivered {delivered,5}" + (source == "dispatched" ? "" : $", first for {wasFirst,5}"));
        }

        PrintLag("dispatched after first arrival", versions.Where(v => firstBy.ContainsKey((v, "dispatched")) && !double.IsNaN(earliest[v]))
            .Select(v => firstBy[(v, "dispatched")] - earliest[v]));
        foreach (var column in appliedColumns)
        {
            PrintLag($"{column} sample at or past it, after first arrival", versions.Where(v => !double.IsNaN(earliest[v]))
                .Select(v => (v, state: states.FirstOrDefault(st => Applied(st, column, v))))
                .Where(p => p.state is not null)
                .Select(p => p.state!.T - earliest[p.v]));
        }

        _out.WriteLine();
        _out.WriteLine("Applied columns come from the SDK state samples, so they are only as precise as the sample interval.");
        _out.WriteLine("A version that is never applied can be a rev-only change, which the bucket does not store.");
    }

    private void PrintLag(string label, IEnumerable<double> lagsMs)
    {
        var lags = lagsMs.Order().ToList();
        if (lags.Count == 0) return;
        double At(double q) => lags[Math.Min(lags.Count - 1, (int)(q * lags.Count))];
        _out.WriteLine($"  {label}, median {S(At(0.5))} s, p95 {S(At(0.95))} s, max {S(lags[^1])} s ({lags.Count} versions)");
    }

    private static bool Applied(RunEvent state, string column, MapVersion v)
    {
        switch (column)
        {
            case "CurrentConfig":
                return state.U64("bucketRev") is { } rev && new MapVersion(state.U64("bucketEpoch") ?? 0, rev).CompareTo(v) >= 0;
            case "KeyMapper":
                return state.U64("keyMapperRev") is { } kmRev && kmRev >= v.Rev;
            default:
                return state.U64("globalRev") is { } globalRev && new MapVersion(state.U64("globalEpoch") ?? 0, globalRev).CompareTo(v) >= 0;
        }
    }

    private void PrintDiffs(string scope)
    {
        Heading($"Changed fields between consecutive {scope} versions");
        var bodies = _run.Arrivals(scope).Where(a => a.Hash is not null)
            .GroupBy(a => a.Version)
            .ToDictionary(g => g.Key, g => g.GroupBy(a => a.Host ?? "").ToDictionary(h => h.Key, h => h.First().Hash!));
        var versions = bodies.Keys.OrderBy(v => v).ToList();
        if (versions.Count < 2)
        {
            _out.WriteLine(versions.Count == 0 ? "No config bodies." : $"Only one version with a body ({versions[0]}).");
            return;
        }

        _out.WriteLine("A one line entry means only rev and vbucket map entries changed.");
        for (var i = 1; i < versions.Count; i++)
        {
            var (before, after) = (bodies[versions[i - 1]], bodies[versions[i]]);

            // Bodies from the same node keep per node fields such as thisNode out of the diff.
            var host = before.Keys.Intersect(after.Keys).OrderBy(h => h, StringComparer.Ordinal).FirstOrDefault();
            var (oldHash, newHash) = host is not null
                ? (before[host], after[host])
                : (before.Values.First(), after.Values.First());
            var title = $"{versions[i - 1]} -> {versions[i]}";
            var oldFlat = Flat(oldHash);
            var newFlat = Flat(newHash);
            if (oldFlat is null || newFlat is null)
            {
                _out.WriteLine($"{title}  a body is missing or not valid JSON");
                continue;
            }

            PrintDiff(title, host, oldFlat, newFlat);
        }
    }

    private void PrintDiff(string title, string? host, Dictionary<string, string> oldFlat, Dictionary<string, string> newFlat)
    {
        var lines = new List<string>();
        var collapsed = new Dictionary<string, int>(StringComparer.Ordinal);
        var onlyRev = true;
        foreach (var path in oldFlat.Keys.Union(newFlat.Keys).Order(StringComparer.Ordinal))
        {
            oldFlat.TryGetValue(path, out var oldValue);
            newFlat.TryGetValue(path, out var newValue);
            if (oldValue == newValue) continue;

            var prefix = CollapsedPrefixes.FirstOrDefault(p => path.StartsWith(p, StringComparison.Ordinal));
            if (prefix is not null)
            {
                var key = prefix.TrimEnd('[');
                collapsed[key] = collapsed.GetValueOrDefault(key) + 1;
                continue;
            }

            onlyRev &= path == "rev";
            lines.Add(oldValue is null ? $"  added    {path} = {Short(newValue)}"
                : newValue is null ? $"  removed  {path} (was {Short(oldValue)})"
                : $"  changed  {path}  {Short(oldValue)} -> {Short(newValue)}");
        }

        var collapsedText = string.Join(", ", collapsed.Select(p => $"{p.Key} {p.Value} entries"));
        if (onlyRev)
        {
            _out.WriteLine($"{title}  {(collapsedText.Length > 0 ? collapsedText : "rev only")}");
            return;
        }

        _out.WriteLine();
        _out.WriteLine($"{title}  ({(host is not null ? $"both from {host}" : "from different nodes")})");
        foreach (var line in lines.Take(MaxDiffLines))
        {
            _out.WriteLine(line);
        }

        if (lines.Count > MaxDiffLines)
        {
            _out.WriteLine($"  ... and {lines.Count - MaxDiffLines} more changed paths");
        }

        if (collapsedText.Length > 0)
        {
            _out.WriteLine($"  changed  {collapsedText}");
        }

        PrintNodeChanges("nodesExt", NodeNames(oldFlat, "nodesExt", "hostname"), NodeNames(newFlat, "nodesExt", "hostname"));
        PrintNodeChanges("nodes", NodeNames(oldFlat, "nodes", "hostname"), NodeNames(newFlat, "nodes", "hostname"));
        _out.WriteLine();
    }

    private void PrintNodeChanges(string list, HashSet<string> before, HashSet<string> after)
    {
        var added = after.Except(before).Order(StringComparer.Ordinal).ToList();
        var removed = before.Except(after).Order(StringComparer.Ordinal).ToList();
        if (added.Count > 0) _out.WriteLine($"  {list} added    {string.Join(", ", added)}");
        if (removed.Count > 0) _out.WriteLine($"  {list} removed  {string.Join(", ", removed)}");
    }

    /// <summary>
    /// The host names of a node list. A nodesExt entry without a hostname is the node that sent the config.
    /// </summary>
    private static HashSet<string> NodeNames(Dictionary<string, string> flat, string list, string field)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var indexes = flat.Keys.Where(k => k.StartsWith(list + "[", StringComparison.Ordinal))
            .Select(k => k[(list.Length + 1)..k.IndexOf(']', StringComparison.Ordinal)])
            .Distinct();
        foreach (var index in indexes)
        {
            names.Add(flat.TryGetValue($"{list}[{index}].{field}", out var host)
                ? host.Trim('"')
                : "(sending node)");
        }

        return names;
    }

    private static string Short(string? value)
    {
        if (value is null) return "";
        return value.Length <= MaxValueLength ? value : value[..MaxValueLength] + "...";
    }

    private Dictionary<string, string>? Flat(string hash, bool cache = true)
    {
        if (_flatBodies.TryGetValue(hash, out var cached)) return cached;
        if (_flatBodies.Count > 64) _flatBodies.Clear();
        Dictionary<string, string>? flat = null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(_run.BodyPath(hash)));
            flat = ConfigJson.Flatten(doc.RootElement);
        }
        catch (Exception)
        {
            // Reported by the caller as a missing body.
        }

        if (cache) _flatBodies[hash] = flat;
        return flat;
    }

    private void PrintPaths()
    {
        var folder = Path.Combine(_run.Folder, "configs");
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.json") : [];
        foreach (var scope in new[] { "cluster", "bucket" })
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var total = 0;
            foreach (var file in files)
            {
                var flat = Flat(Path.GetFileNameWithoutExtension(file), cache: false);
                if (flat is null) continue;
                var name = flat.TryGetValue("name", out var n) ? n.Trim('"') : null;
                if (RunFolder.ScopeOf(name) != scope) continue;

                total++;
                foreach (var path in flat.Keys.Select(ConfigJson.Normalize).Distinct())
                {
                    counts[path] = counts.GetValueOrDefault(path) + 1;
                }
            }

            Heading($"JSON paths in {scope} config bodies ({total} distinct bodies, count = bodies with the path)");
            foreach (var (path, count) in counts.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                _out.WriteLine($"{count,6}  {path}");
            }
        }
    }

    private void PrintDns()
    {
        Heading("DNS SRV");
        var dns = _events.Where(e => e.Stream == "dns").ToList();
        if (dns.Count == 0)
        {
            _out.WriteLine("No DNS events.");
            return;
        }

        foreach (var e in dns)
        {
            var detail = e.Kind switch
            {
                "records" => string.Join(", ", e.E.GetProperty("records").EnumerateArray()
                    .Select(r => $"{r.GetProperty("target").GetString()}:{r.GetProperty("port").GetInt32()}")),
                "skipped" => e.Str("reason"),
                "start" => e.Str("query"),
                _ => $"{e.Str("error")} {e.Str("detail")}",
            };
            _out.WriteLine($"{S(e.T),10} {e.Kind,-8} {detail}");
        }
    }

    private void PrintLoad()
    {
        Heading("Load");
        var stats = _events.Where(e => e.Stream == "load" && e.Kind == "stats").ToList();
        if (stats.Count == 0)
        {
            _out.WriteLine(_events.Any(e => e.Stream == "load" && e.Kind == "off") ? "Load was off." : "No load stats.");
            return;
        }

        var ops = stats.Sum(s => (long)(s.U64("ops") ?? 0));
        var seconds = stats.Sum(s => s.Dbl("seconds") ?? 0);
        var peak = stats.Max(s => (s.U64("ops") ?? 0) / Math.Max(s.Dbl("seconds") ?? 1, 0.001));
        var errors = new Dictionary<string, long>(StringComparer.Ordinal);
        var secondsWithErrors = 0;
        foreach (var s in stats)
        {
            if (!s.E.TryGetProperty("errors", out var errorObject)) continue;
            var any = false;
            foreach (var p in errorObject.EnumerateObject())
            {
                errors[p.Name] = errors.GetValueOrDefault(p.Name) + p.Value.GetInt64();
                any = true;
            }

            if (any) secondsWithErrors++;
        }

        _out.WriteLine($"Operations         {ops} in {seconds:0} s, average {ops / Math.Max(seconds, 0.001):0} per s, peak {peak:0} per s");
        _out.WriteLine($"Misses             {stats.Sum(s => (long)(s.U64("misses") ?? 0))} (get on a key not written yet)");
        _out.WriteLine($"Not-my-vbucket     {stats.Sum(s => (long)(s.U64("nmvb") ?? 0))} responses seen by the tap, {_events.Count(e => e.Stream == "tap" && e.Kind == "nmvb")} with a config body");
        _out.WriteLine($"Max latency        {stats.Max(s => s.Dbl("maxMs") ?? 0):0.0} ms");
        _out.WriteLine($"Seconds w/ errors  {secondsWithErrors}");
        foreach (var (type, count) in errors.OrderByDescending(p => p.Value))
        {
            _out.WriteLine($"  {type,-40} {count,8}");
        }
    }

    private void PrintErrors()
    {
        Heading("Recorder errors");
        var errors = _events.Where(e => e.Stream == "recorder" && e.Kind == "error").ToList();
        if (errors.Count == 0)
        {
            _out.WriteLine("None.");
            return;
        }

        foreach (var e in errors.Take(30))
        {
            _out.WriteLine($"{S(e.T),10} [{e.Str("component")}] {e.Str("message")} {e.Str("error")} {e.Str("detail")}");
        }

        if (errors.Count > 30)
        {
            _out.WriteLine($"... and {errors.Count - 30} more");
        }
    }
}
