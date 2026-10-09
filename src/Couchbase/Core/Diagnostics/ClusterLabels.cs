using System;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.Core.Diagnostics;
#nullable enable
internal class ClusterLabels
{
    public string? ClusterUuid { get; set; }
    public string? ClusterName { get; set; }

    /// <summary>
    /// The config version the name was last taken from, so an older config arriving late cannot revert it.
    /// </summary>
    public ConfigVersion NameVersion { get; set; }

    /// <summary>
    /// The config version the UUID was last taken from. It is tracked apart from the name, because a config
    /// can supply one label and not the other.
    /// </summary>
    public ConfigVersion UuidVersion { get; set; }

    public bool Equals(ClusterLabels other)
    {
        return ClusterUuid == other.ClusterUuid && ClusterName == other.ClusterName;
    }
}
