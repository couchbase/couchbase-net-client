using System.Diagnostics;
using Couchbase.Core.Diagnostics.Tracing;

#nullable enable
namespace Couchbase.Core.Diagnostics.Metrics;

internal static class MetricsExtensions
{
    internal static void AddClusterLabelsIfProvided(ref this TagList tagList, IRequestSpan? span)
    {
        if (span is RequestSpanWrapper wrapper)
        {
            if (wrapper.ClusterLabels?.ClusterName is not null)
            {
                tagList.Add(OuterRequestSpans.Attributes.ClusterName, wrapper.ClusterLabels.ClusterName);
            }
            if (wrapper.ClusterLabels?.ClusterUuid is not null)
            {
                tagList.Add(OuterRequestSpans.Attributes.ClusterUuid, wrapper.ClusterLabels.ClusterUuid);
            }
        }
    }
}
