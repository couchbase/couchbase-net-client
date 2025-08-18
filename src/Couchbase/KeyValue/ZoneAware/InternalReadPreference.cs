namespace Couchbase.KeyValue.ZoneAware;

public enum InternalReadPreference
{
    /// <summary>
    /// Performs the operation on all server groups.
    /// </summary>
    NoPreference,
    /// <summary>
    /// Performs the zone-aware operation on the preferred server group only.
    /// </summary>
    SelectedServerGroup,
    /// <summary>
    /// Attempts to perform the zone-aware operation on the preferred server group, falling back
    /// to all available server groups if no replicas in the preferred server group are available.
    /// </summary>
    SelectedServerGroupWithFallback
}
