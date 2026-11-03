using System.Text.Json;

namespace Couchbase.ConfigRecorder;

/// <summary>
/// The events of a run folder sorted by time, and the config versions that they deliver.
/// </summary>
internal sealed class RunFolder
{
    public const string ProbeBody = "probe body";

    private RunFolder(string folder, List<RunEvent> events, int badLines)
    {
        Folder = folder;
        Events = events;
        BadLines = badLines;
        var start = events.FirstOrDefault(e => e.Stream == "recorder" && e.Kind == "start");
        if (start is not null && start.E.TryGetProperty("settings", out var settings)
                              && settings.TryGetProperty("bucket", out var bucket))
        {
            BucketName = bucket.GetString();
        }
    }

    public string Folder { get; }

    public IReadOnlyList<RunEvent> Events { get; }

    public int BadLines { get; }

    public string? BucketName { get; }

    public static string EventsPath(string folder) => Path.Combine(folder, "events.jsonl");

    /// <summary>
    /// Reads events.jsonl. Lines that are not valid JSON are counted in <see cref="BadLines"/> and skipped.
    /// </summary>
    public static RunFolder Load(string folder)
    {
        var events = new List<RunEvent>();
        var badLines = 0;
        foreach (var line in File.ReadLines(EventsPath(folder)))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement.Clone();
                events.Add(new RunEvent(
                    root.GetProperty("t").GetDouble(),
                    root.GetProperty("ts").GetString() ?? "",
                    root.GetProperty("stream").GetString() ?? "",
                    root.GetProperty("kind").GetString() ?? "",
                    root));
            }
            catch (Exception)
            {
                badLines++;
            }
        }

        // A stable sort keeps file order for events with the same time.
        var sorted = events.OrderBy(e => e.T).ToList();
        return new RunFolder(folder, sorted, badLines);
    }

    public string BodyPath(string hash) => Path.Combine(Folder, "configs", hash + ".json");

    public static string ScopeOf(string? name) => name is null || name == "CLUSTER" ? "cluster" : "bucket";

    /// <summary>
    /// Every version of a scope that any source delivered.
    /// </summary>
    public List<MapVersion> AllVersions(string scope) =>
        Arrivals(scope).Select(a => a.Version).Distinct().OrderBy(v => v).ToList();

    public IEnumerable<Arrival> Arrivals(string scope)
    {
        foreach (var e in Events)
        {
            switch (e.Stream, e.Kind)
            {
                case ("tap" or "probe-tap", "push"):
                {
                    var brief = e.Bool("brief") == true;
                    var pushScope = string.IsNullOrEmpty(e.Str("pushBucket")) ? "cluster" : "bucket";
                    if (pushScope != scope) break;
                    var v = e.U64("bodyRev") is { } bodyRev
                        ? new MapVersion(e.U64("bodyRevEpoch") ?? 0, bodyRev)
                        : new MapVersion(e.U64("epoch") ?? 0, e.U64("rev") ?? 0);
                    var source = (e.Stream == "probe-tap" ? "probe " : "") + (brief ? "brief push" : "full push");
                    yield return new Arrival(source, v, e.T, e.Str("host"), e.Str("hash"), e);
                    break;
                }
                case ("tap" or "probe-tap", "config" or "nmvb"):
                {
                    if (e.Str("hash") is null || e.U64("bodyRev") is not { } rev) break;
                    if (ScopeOf(e.Str("name")) != scope) break;
                    // Probe fetch bodies only feed the field diffs. The probe column comes from probe version events.
                    var source = e.Stream == "probe-tap" ? ProbeBody : e.Kind == "nmvb" ? "nmvb body" : "fetch reply";

                    yield return new Arrival(source, new MapVersion(e.U64("bodyRevEpoch") ?? 0, rev), e.T, e.Str("host"), e.Str("hash"), e);
                    break;
                }
                case ("probe", "version"):
                    if (e.Str("scope") != scope) break;
                    yield return new Arrival("probe", new MapVersion(e.U64("epoch") ?? 0, e.U64("rev") ?? 0), e.T, e.Str("node"), null, e);
                    break;
                case ("sdk", "dispatch"):
                    if (ScopeOf(e.Str("name")) != scope) break;
                    yield return new Arrival("dispatched", new MapVersion(e.U64("epoch") ?? 0, e.U64("rev") ?? 0), e.T, null, null, e);
                    break;
            }
        }
    }
}

internal sealed record RunEvent(double T, string Ts, string Stream, string Kind, JsonElement E)
{
    public string? Str(string name) =>
        E.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public ulong? U64(string name) =>
        E.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetUInt64(out var n) ? n : null;

    public double? Dbl(string name) =>
        E.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    public bool? Bool(string name) =>
        E.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
}

internal readonly record struct MapVersion(ulong Epoch, ulong Rev) : IComparable<MapVersion>
{
    public int CompareTo(MapVersion other) => Epoch != other.Epoch ? Epoch.CompareTo(other.Epoch) : Rev.CompareTo(other.Rev);

    public override string ToString() => $"{Epoch}/{Rev}";
}

/// <summary>
/// One delivery of a config version. <see cref="Event"/> is the event that delivered it.
/// </summary>
internal readonly record struct Arrival(string Source, MapVersion Version, double T, string? Host, string? Hash, RunEvent Event);
