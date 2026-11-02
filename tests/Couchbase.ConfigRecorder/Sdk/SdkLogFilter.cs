namespace Couchbase.ConfigRecorder.Sdk;

/// <summary>
/// Drops the Debug lines that the SDK writes for every KV operation. Under load they add up to
/// gigabytes per hour and slow the SDK down. All other lines are kept.
/// </summary>
internal static class SdkLogFilter
{
    private const string ClusterNode = "Couchbase.Core.ClusterNode";
    private const string CouchbaseBucket = "Couchbase.CouchbaseBucket";

    // Executing op, completed op, circuit breaker state. Event 101, a failed status, is kept.
    private static readonly HashSet<int> ClusterNodePerOperation = [100, 103, 200];

    public static bool Keep(string category, Microsoft.Extensions.Logging.EventId eventId, object? state)
    {
        if (category == ClusterNode)
        {
            return !ClusterNodePerOperation.Contains(eventId.Id);
        }

        if (category == CouchbaseBucket && eventId.Id == 0
            && state is IReadOnlyList<KeyValuePair<string, object?>> { Count: > 0 } values
            && values[^1].Value is string format
            && format.StartsWith("Mapping op ", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
