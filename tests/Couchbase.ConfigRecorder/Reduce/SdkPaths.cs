using System.Collections;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.ConfigRecorder.Reduce;

/// <summary>
/// The JSON leaf paths that the SDK reads into <see cref="BucketConfig"/>, found by reflection.
/// Arrays and lists add <c>[]</c> and dictionary keys show as <c>*</c>.
/// </summary>
internal sealed class SdkPaths
{
    private readonly List<(string Path, Regex Match)> _paths;

    private SdkPaths(IEnumerable<string> paths)
    {
        _paths = paths.Distinct().Order(StringComparer.Ordinal)
            .Select(p => (p, new Regex("^" + Regex.Escape(p).Replace(@"\*", @"[^.\[]+") + "$", RegexOptions.CultureInvariant)))
            .ToList();
    }

    public IReadOnlyList<string> All => _paths.Select(p => p.Path).ToList();

    public static SdkPaths ForBucketConfig()
    {
        var paths = new List<string>();
        Walk(typeof(BucketConfig), "", paths, []);
        return new SdkPaths(paths);
    }

    /// <summary>
    /// True when the SDK has a property for a body path. An empty object or array in a body is a leaf
    /// at the container path, so a container of SDK paths counts too.
    /// </summary>
    public bool Models(string bodyPath) =>
        _paths.Any(p => p.Match.IsMatch(bodyPath) || p.Match.IsMatch(bodyPath + "[]"))
        || _paths.Any(p => IsContainerOf(bodyPath, p.Path));

    /// <summary>
    /// The SDK paths that no body path matches.
    /// </summary>
    public List<string> NotIn(IReadOnlyCollection<string> bodyPaths) =>
        _paths.Where(p => !bodyPaths.Any(b => p.Match.IsMatch(b) || p.Match.IsMatch(b + "[]")))
            .Select(p => p.Path)
            .ToList();

    private static bool IsContainerOf(string container, string path) =>
        path.Length > container.Length && path.StartsWith(container, StringComparison.Ordinal)
                                       && path[container.Length] is '.' or '[';

    private static void Walk(Type type, string prefix, List<string> paths, HashSet<Type> visiting)
    {
        if (IsLeaf(type))
        {
            paths.Add(prefix);
            return;
        }

        if (type.IsArray)
        {
            Walk(type.GetElementType()!, prefix + "[]", paths, visiting);
            return;
        }

        if (DictionaryValueType(type) is { } valueType)
        {
            Walk(valueType, prefix + ".*", paths, visiting);
            return;
        }

        if (EnumerableItemType(type) is { } itemType)
        {
            Walk(itemType, prefix + "[]", paths, visiting);
            return;
        }

        if (!visiting.Add(type)) return;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (JsonName(property) is not { } name) continue;
            Walk(property.PropertyType, prefix.Length == 0 ? name : $"{prefix}.{name}", paths, visiting);
        }

        visiting.Remove(type);
    }

    /// <summary>
    /// The name that the SDK serializer binds. Without [JsonPropertyName] it binds a public settable property
    /// by its camel case name, because the SDK context uses the camel case policy.
    /// </summary>
    private static string? JsonName(PropertyInfo property)
    {
        if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null) return null;
        if (property.GetCustomAttribute<JsonPropertyNameAttribute>() is { } attribute) return attribute.Name;
        if (property.GetIndexParameters().Length > 0 || property.SetMethod is not { IsPublic: true }) return null;
        return char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
    }

    private static bool IsLeaf(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
    }

    private static Type? DictionaryValueType(Type type) =>
        SelfAndInterfaces(type)
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            ?.GetGenericArguments()[1];

    private static Type? EnumerableItemType(Type type) =>
        !typeof(IEnumerable).IsAssignableFrom(type)
            ? null
            : SelfAndInterfaces(type)
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];

    private static IEnumerable<Type> SelfAndInterfaces(Type type) => type.IsInterface ? type.GetInterfaces().Prepend(type) : type.GetInterfaces();
}
