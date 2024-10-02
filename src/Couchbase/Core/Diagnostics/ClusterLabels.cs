using System;

namespace Couchbase.Core.Diagnostics;
#nullable enable
internal class ClusterLabels
{
    public string? ClusterUuid { get; set; }
    public string? ClusterName { get; set; }
    public bool Equals(ClusterLabels other)
    {
        return ClusterUuid == other.ClusterUuid && ClusterName == other.ClusterName;
    }
}
