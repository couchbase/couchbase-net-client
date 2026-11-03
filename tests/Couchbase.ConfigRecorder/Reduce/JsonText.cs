using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Couchbase.ConfigRecorder.Reduce;

/// <summary>
/// Writes JSON that is easy to read and diff. Objects get one member per line. A container of primitives,
/// or an object of primitives and primitive arrays, stays on one line when it is short.
/// The output depends only on the node, so the same input gives the same bytes.
/// </summary>
internal static class JsonText
{
    private const int MaxInline = 160;

    private static readonly JsonSerializerOptions ValueOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Write(JsonNode? node)
    {
        var builder = new StringBuilder();
        Write(node, builder, 0);
        builder.Append('\n');
        return builder.ToString();
    }

    public static string Inline(JsonNode? node)
    {
        var builder = new StringBuilder();
        WriteInline(node, builder);
        return builder.ToString();
    }

    public static void WriteFile(string path, JsonNode? node) =>
        File.WriteAllText(path, Write(node), new UTF8Encoding(false));

    private static void Write(JsonNode? node, StringBuilder builder, int depth)
    {
        if (IsSimple(node))
        {
            var inline = Inline(node);
            if (node is not (JsonObject or JsonArray) || inline.Length <= MaxInline)
            {
                builder.Append(inline);
                return;
            }
        }

        var pad = new string(' ', (depth + 1) * 2);
        switch (node)
        {
            case JsonObject obj:
                builder.Append('{');
                var firstMember = true;
                foreach (var (name, value) in obj)
                {
                    builder.Append(firstMember ? "\n" : ",\n").Append(pad);
                    firstMember = false;
                    builder.Append(JsonSerializer.Serialize(name, ValueOptions)).Append(": ");
                    Write(value, builder, depth + 1);
                }

                builder.Append('\n').Append(' ', depth * 2).Append('}');
                break;
            case JsonArray array:
                builder.Append('[');
                for (var i = 0; i < array.Count; i++)
                {
                    builder.Append(i == 0 ? "\n" : ",\n").Append(pad);
                    Write(array[i], builder, depth + 1);
                }

                builder.Append('\n').Append(' ', depth * 2).Append(']');
                break;
        }
    }

    private static void WriteInline(JsonNode? node, StringBuilder builder)
    {
        switch (node)
        {
            case JsonObject obj:
                builder.Append('{');
                var first = true;
                foreach (var (name, value) in obj)
                {
                    if (!first) builder.Append(", ");
                    first = false;
                    builder.Append(JsonSerializer.Serialize(name, ValueOptions)).Append(": ");
                    WriteInline(value, builder);
                }

                builder.Append('}');
                break;
            case JsonArray array:
                builder.Append('[');
                for (var i = 0; i < array.Count; i++)
                {
                    if (i > 0) builder.Append(", ");
                    WriteInline(array[i], builder);
                }

                builder.Append(']');
                break;
            case null:
                builder.Append("null");
                break;
            default:
                builder.Append(node.ToJsonString(ValueOptions));
                break;
        }
    }

    private static bool IsSimple(JsonNode? node) => node switch
    {
        JsonArray array => array.All(IsPrimitive),
        JsonObject obj => obj.All(p => IsPrimitive(p.Value) || p.Value is JsonArray a && a.All(IsPrimitive)),
        _ => true,
    };

    private static bool IsPrimitive(JsonNode? node) => node is not (JsonObject or JsonArray);
}
