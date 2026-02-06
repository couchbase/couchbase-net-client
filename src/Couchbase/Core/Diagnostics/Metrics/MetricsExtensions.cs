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
            var labels = wrapper.ClusterLabels;
            if (labels is not null)
            {
                var clusterName = labels.ClusterName;
                if (clusterName is not null)
                {
                    tagList.Add(OuterRequestSpans.Attributes.ClusterName, clusterName);
                }

                var clusterUuid = labels.ClusterUuid;
                if (clusterUuid is not null)
                {
                    tagList.Add(OuterRequestSpans.Attributes.ClusterUuid, clusterUuid);
                }
            }
        }
    }
}
