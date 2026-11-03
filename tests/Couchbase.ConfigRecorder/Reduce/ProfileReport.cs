using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Couchbase.ConfigRecorder.Reduce;

/// <summary>
/// Writes profile.md from the profile JSON, so the report never says more than profile.json.
/// </summary>
internal static class ProfileReport
{
    private const int MaxSampleLength = 60;

    public static string Write(JsonObject profile)
    {
        var md = new StringBuilder();
        var source = profile["source"]!.AsObject();
        md.Append("# Profile ").Append(profile["name"]).Append("\n\n");
        md.Append("The `reduce` command of the config recorder writes this file from profile.json. Do not edit it.\n");

        Source(md, source);
        Varying(md, profile["fields"]!.AsObject());
        SdkGaps(md, profile["fields"]!.AsObject());
        Nodes(md, profile["nodeShape"]!.AsObject());
        Transitions(md, profile["transitions"]!.AsObject());
        Examples(md, profile["examples"]!.AsArray());
        return md.ToString();
    }

    private static void Source(StringBuilder md, JsonObject source)
    {
        md.Append("\n## Source\n\n");
        md.Append($"Server {source["serverVersion"]?.ToString() ?? "version unknown"}, captured {source["captureDate"]}.\n\n");
        md.Append("| Run | TLS | Length (s) |\n| --- | --- | --- |\n");
        foreach (var run in source["runs"]!.AsArray())
        {
            md.Append($"| {run!["folder"]} | {Yes(run["tls"])} | {run["seconds"]} |\n");
        }

        md.Append("\n| Kind | Distinct bodies | Versions with a body |\n| --- | --- | --- |\n");
        foreach (var (kind, count) in source["bodies"]!.AsObject())
        {
            md.Append($"| {kind} | {count} | {source["versionsWithBody"]![kind]} |\n");
        }
    }

    private static void Varying(StringBuilder md, JsonObject fields)
    {
        md.Append("\n## Fields that vary\n\n");
        md.Append("perNode means the nodes of one body disagree, or some nodes do not have the field. ");
        md.Append("perVersion means the field changes between bodies, or some bodies do not have it. ");
        md.Append("All other fields are constant and the template holds them.\n\n");
        md.Append("| Kind | Path | Scope | Distinct | Share | Samples |\n| --- | --- | --- | --- | --- | --- |\n");
        foreach (var (kind, field) in Fields(fields).Where(f => f.Field["scope"]!.ToString() != "constant"))
        {
            md.Append($"| {kind} | `{field["path"]}` | {field["scope"]} | {field["distinct"]} | {field["share"]} | {Samples(field)} |\n");
        }
    }

    private static void SdkGaps(StringBuilder md, JsonObject fields)
    {
        md.Append("\n## Fields the SDK does not model\n\n");
        md.Append("The SDK config classes have no property for these paths, so the SDK drops them.\n\n");
        md.Append("| Kind | Path | Scope | Samples |\n| --- | --- | --- | --- |\n");
        foreach (var (kind, field) in Fields(fields).Where(f => f.Field["modeledBySdk"]!.GetValue<bool>() == false))
        {
            md.Append($"| {kind} | `{field["path"]}` | {field["scope"]} | {Samples(field)} |\n");
        }

        md.Append("\n## SDK fields the server never sends\n\n");
        md.Append("No body of this profile has these paths. The SDK keeps their default values.\n\n");
        foreach (var path in fields["sdkPathsNotSent"]!.AsArray())
        {
            md.Append($"- `{path}`\n");
        }
    }

    private static void Nodes(StringBuilder md, JsonObject shape)
    {
        md.Append("\n## Nodes\n\n");
        md.Append("Ports in nodesExt.services, the same on every node unless a list is shown.\n\n");
        md.Append("| Key | Port |\n| --- | --- |\n");
        foreach (var (key, port) in shape["ports"]!.AsObject())
        {
            md.Append($"| {key} | {JsonText.Inline(port)} |\n");
        }

        md.Append("\n| Service set | Services | Keys |\n| --- | --- | --- |\n");
        foreach (var set in shape["serviceSets"]!.AsArray())
        {
            md.Append($"| {set!["id"]} | {Join(set["services"])} | {set["keys"]!.AsArray().Count} |\n");
        }

        md.Append("\nService sets over the life of each member. A member is one node with one nodeUUID.\n\n");
        md.Append("| Service sets in order | Joined during a run | Left during a run | Members |\n| --- | --- | --- | --- |\n");
        foreach (var lifecycle in shape["lifecycles"]!.AsArray())
        {
            md.Append($"| {string.Join(" then ", lifecycle!["serviceSets"]!.AsArray().Select(s => s!.ToString()))} | {Yes(lifecycle["joined"])} | {Yes(lifecycle["left"])} | {Join(lifecycle["members"])} |\n");
        }

        var groups = shape["serverGroups"]!.AsObject();
        md.Append($"\nServer groups follow the pattern `{groups["pattern"]?.ToString() ?? "none"}` with the values {Join(groups["distinct"])}.\n");
        md.Append($"In join order the members are in {Join(groups["perMember"])}.\n");

        var alternates = shape["alternateAddresses"]!.AsObject();
        if (alternates.Count == 0)
        {
            md.Append("\nNo node has alternate addresses.\n");
            return;
        }

        foreach (var (network, entry) in alternates)
        {
            md.Append($"\nAlternate network `{network}` is on {entry!["members"]} members. ");
            md.Append(entry["hostname"]!.ToString() == "shared"
                ? $"All of them share the host name `{entry["sharedHostname"]}`.\n\n"
                : "Each member has its own host name.\n\n");
            md.Append("| Port key | Value |\n| --- | --- |\n");
            foreach (var (key, port) in entry["ports"]!.AsObject())
            {
                var value = port!["shared"] is { } shared
                    ? $"shared {shared}"
                    : $"per member in join order {JsonText.Inline(port["perNode"])}" + (port["contiguousFrom"] is { } from ? $", a range from {from}" : "");
                md.Append($"| {key} | {value} |\n");
            }
        }

        if (shape["nodes"]?["alternateAddresses"] is JsonObject legacy)
        {
            foreach (var (network, entry) in legacy)
            {
                md.Append($"\nThe bucket nodes[] list also has alternate network `{network}`, host name {entry!["hostname"]}");
                md.Append(entry["sharedHostname"] is { } host ? $" `{host}`" : "");
                md.Append(entry["portKeys"]!.AsArray().Count == 0 ? ", no ports.\n" : $", ports {Join(entry["portKeys"])}.\n");
            }
        }
    }

    private static void Transitions(StringBuilder md, JsonObject transitions)
    {
        md.Append("\n## Transitions\n\n");
        md.Append("Each step compares the first body of a version with the first body of the version before it, without thisNode.\n");
        foreach (var (kind, node) in transitions)
        {
            var t = node!.AsObject();
            md.Append($"\n### {kind}\n\n");
            md.Append($"{t["steps"]} steps between {t["versionsWithBody"]} versions with a body. Any source delivered {t["versionsSeen"]} versions.\n");
            md.Append($"Gap between versions with a body {Distribution(t["stepGapMs"])} ms. Gap between all versions {Distribution(t["versionGapMs"])} ms.\n");
            if (t["vbucketsMovedPerStep"] is JsonObject moved && moved["count"]!.GetValue<int>() > 0)
            {
                md.Append($"Steps that move vbuckets move {Distribution(moved)} vbuckets, {moved["total"]} in total.\n");
            }

            md.Append("\n| Label | Steps |\n| --- | --- |\n");
            foreach (var (label, count) in t["labels"]!.AsObject())
            {
                md.Append($"| {label} | {count} |\n");
            }

            var other = t["otherPaths"]!.AsObject();
            if (other.Count > 0)
            {
                md.Append("\nOther changed paths ").Append(string.Join(", ", other.Select(p => $"`{p.Key}` in {p.Value} steps"))).Append(".\n");
            }

            if (t["rebalances"] is JsonArray rebalances && rebalances.Count > 0)
            {
                md.Append("\nRebalances. One rebalance is the versions with a forward map, the node additions before them and the node removals after them.\n\n");
                md.Append("| Run | From rev | To rev | Versions with a body | Versions seen | With forward map | Seconds | vbuckets moved | Active moved |\n");
                md.Append("| --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");
                foreach (var r in rebalances)
                {
                    md.Append($"| {r!["run"]} | {r["fromRev"]} | {r["toRev"]?.ToString() ?? "not finished"} | {r["versionsWithBody"]} | {r["versionsSeen"]} | {r["forwardMapVersions"]} | {r["seconds"]} | {r["vbucketsMoved"]} | {r["activeMoved"]} |\n");
                }
            }

            var missing = t["missingThisNode"]!.AsArray();
            md.Append(missing.Count == 0 ? "\nEvery body has thisNode.\n" : "\nBodies without thisNode.\n\n");
            if (missing.Count == 0) continue;
            md.Append("| Run | Rev | Sent by | Sender in nodesExt | Source | First body of the version |\n| --- | --- | --- | --- | --- | --- |\n");
            foreach (var m in missing)
            {
                md.Append($"| {m!["run"]} | {m["rev"]} | {m["servedBy"]} | {Yes(m["servedByInNodesExt"])} | {m["source"]} | {Yes(m["firstBodyOfVersion"])} |\n");
            }
        }
    }

    private static void Examples(StringBuilder md, JsonArray examples)
    {
        md.Append("\n## Examples\n\n");
        md.Append("| File | Rev | nodesExt | nodes | Forward map | thisNode | Alternate addresses | Same services |\n");
        md.Append("| --- | --- | --- | --- | --- | --- | --- | --- |\n");
        foreach (var e in examples)
        {
            md.Append($"| {e!["file"]} | {e["rev"]} | {e["nodesExt"]} | {e["nodes"]} | {Yes(e["forwardMap"])} | {Yes(e["thisNode"])} | {Yes(e["alternateAddresses"])} | {Yes(e["uniformServices"])} |\n");
        }
    }

    private static IEnumerable<(string Kind, JsonObject Field)> Fields(JsonObject fields) =>
        fields.Where(p => p.Key is "cluster" or "bucket").SelectMany(p => p.Value!.AsArray().Select(f => (p.Key, f!.AsObject())));

    private static string Samples(JsonObject field)
    {
        var text = string.Join(", ", field["samples"]!.AsArray().Select(s => JsonText.Inline(s).Replace("|", "\\|", StringComparison.Ordinal)));
        return text.Length <= MaxSampleLength ? text : text[..MaxSampleLength] + "...";
    }

    private static string Distribution(JsonNode? node) =>
        node is JsonObject d && d["count"]!.GetValue<int>() > 0
            ? string.Create(CultureInfo.InvariantCulture, $"median {d["median"]}, p95 {d["p95"]}, max {d["max"]}")
            : "not measured";

    private static string Join(JsonNode? array) =>
        string.Join(", ", array!.AsArray().Select(v => v is null ? "none" : v.ToString()));

    private static string Yes(JsonNode? value) => value is null ? "unknown" : value.GetValue<bool>() ? "yes" : "no";
}
