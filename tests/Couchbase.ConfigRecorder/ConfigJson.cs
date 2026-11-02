using System.Text.Json;

namespace Couchbase.ConfigRecorder;

internal static class ConfigJson
{
    public readonly record struct Version(ulong? Rev, ulong? RevEpoch, string? Name, string? Error);

    public static Version ReadVersion(byte[] body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new Version(null, null, null, "The body is not a JSON object.");
            }

            return new Version(ReadUInt64(root, "rev"), ReadUInt64(root, "revEpoch"),
                root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null,
                null);
        }
        catch (Exception e)
        {
            return new Version(null, null, null, e.Message);
        }
    }

    private static ulong? ReadUInt64(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out var number)
            ? number
            : null;

    /// <summary>
    /// Flattens a JSON document to leaf paths such as <c>nodesExt[2].services.kv</c> and their raw JSON values.
    /// Empty objects and arrays count as leaves.
    /// </summary>
    public static Dictionary<string, string> Flatten(JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        Flatten(root, "", result);
        return result;
    }

    private static void Flatten(JsonElement element, string path, Dictionary<string, string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var any = false;
                foreach (var property in element.EnumerateObject())
                {
                    any = true;
                    Flatten(property.Value, path.Length == 0 ? property.Name : $"{path}.{property.Name}", result);
                }

                if (!any) result[path] = "{}";
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Flatten(item, $"{path}[{index}]", result);
                    index++;
                }

                if (index == 0) result[path] = "[]";
                break;
            default:
                result[path] = element.GetRawText();
                break;
        }
    }

    /// <summary>
    /// Replaces array indexes with [] so paths of all list items count as one.
    /// </summary>
    public static string Normalize(string path)
    {
        var builder = new System.Text.StringBuilder(path.Length);
        var inIndex = false;
        foreach (var c in path)
        {
            if (c == '[')
            {
                inIndex = true;
                builder.Append("[]");
            }
            else if (c == ']')
            {
                inIndex = false;
            }
            else if (!inIndex)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
