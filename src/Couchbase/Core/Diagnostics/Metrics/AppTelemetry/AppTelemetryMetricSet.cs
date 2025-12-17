using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text;
using Couchbase.Core.Compatibility;
using Couchbase.Utils;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

internal class AppTelemetryMetricSet
{
    private ConcurrentDictionary<AppTelemetryRequestType, AppTelemetryHistogramValue> Histograms { get; } = new();
    private ConcurrentDictionary<AppTelemetryServiceType, AppTelemetryCounterValue> Counters { get; } = new();

    public AppTelemetryMetricSet()
    {
    }

    public void IncrementHistogram(AppTelemetryRequestType requestType, TimeSpan operationLatency)
    {
        var histogram = Histograms.GetOrAdd(requestType, type => new AppTelemetryHistogramValue(type));
        histogram.IncrementCountAndSum(operationLatency);
    }

    public void IncrementCounter(AppTelemetryServiceType serviceType, AppTelemetryCounterType counterType)
    {
        var serviceCounters = Counters.GetOrAdd(serviceType, _ => new AppTelemetryCounterValue());
        serviceCounters.Increment(counterType);
    }

    public string ExportAllMetrics(NodeAndBucket nodeAndBucket)
    {
        var sb = new StringBuilder();
        if (TryExportCountersAsPrometheus(nodeAndBucket, out var counters)) sb.Append(counters);
        if (TryExportHistogramsAsPrometheus(nodeAndBucket, out var histograms)) sb.Append(histograms);
        return sb.ToString();
    }

    private bool TryExportCountersAsPrometheus(NodeAndBucket nodeAndBucket, out string exported)
    {
        var sb = new StringBuilder();
        exported = string.Empty;

        if (Counters.IsEmpty) return false;

        // No need to take a snapshot of the collection here, since the AppTelemetryCollector
        // already takes a snapshot of the MetricsSets.
        foreach (var serviceEntry in Counters)
        {
            var serviceType = serviceEntry.Key;
            var counterValue = serviceEntry.Value;

            var serviceDesc = serviceType.GetDescription();

            var bucketLabel = serviceType == AppTelemetryServiceType.KeyValue && !string.IsNullOrEmpty(nodeAndBucket.Bucket)
                ? $",bucket=\"{nodeAndBucket.Bucket}\""
                : "";
            var alternateAddressLabel = !string.IsNullOrEmpty(nodeAndBucket.AlternateNode)
                ? $",alt_node=\"{nodeAndBucket.AlternateNode}\""
                : "";

            var baseLabels = $"{{agent=\"{AppTelemetryUtils.Agent}\",node=\"{nodeAndBucket.Node}\",node_uuid=\"{nodeAndBucket.NodeUuid}\"{alternateAddressLabel}{bucketLabel}}}";

            sb.AppendLine($"sdk_{serviceDesc}_r_timedout{baseLabels} {counterValue.TimedOut}");
            sb.AppendLine($"sdk_{serviceDesc}_r_canceled{baseLabels} {counterValue.Canceled}");
            sb.AppendLine($"sdk_{serviceDesc}_r_total{baseLabels} {counterValue.Total}");
        }

        exported = sb.ToString();
        return true;
    }

    private bool TryExportHistogramsAsPrometheus(NodeAndBucket nodeAndBucket, out string exported)
    {
        var sb = new StringBuilder();
        exported = string.Empty;

        if (Histograms.IsEmpty) return false;

        foreach (var histogramEntry in Histograms)
        {
            var histogramType = histogramEntry.Key;
            var histogramValue = histogramEntry.Value;

            var metricName = histogramType.GetDescription();

            uint cumulativeCount = 0;
            double cumulativeSum = 0;

            var bucketLabel = !string.IsNullOrEmpty(nodeAndBucket.Bucket)
                ? $",bucket=\"{nodeAndBucket.Bucket}\""
                : "";
            var alternateAddressLabel = !string.IsNullOrEmpty(nodeAndBucket.AlternateNode)
                ? $",alt_node=\"{nodeAndBucket.AlternateNode}\""
                : "";

            foreach (var bin in histogramValue.AppTelemetryHistogramBins.Bins)
            {
                var le = bin.Keys.First();
                var value = bin[le];
                cumulativeCount += value.Key;
                cumulativeSum += value.Value;

                sb.AppendLine($"{metricName}_duration_milliseconds_bucket{{le=\"{(double.IsPositiveInfinity(le) ? "+Inf" : le.ToString(CultureInfo.InvariantCulture))}\",agent=\"{AppTelemetryUtils.Agent}\"{bucketLabel},node=\"{nodeAndBucket.Node}\",node_uuid=\"{nodeAndBucket.NodeUuid}\"{alternateAddressLabel}}} {cumulativeCount}");
            }

            sb.AppendLine($"{metricName}_duration_milliseconds_sum{{agent=\"{AppTelemetryUtils.Agent}\"{bucketLabel},node=\"{nodeAndBucket.Node}\",node_uuid=\"{nodeAndBucket.NodeUuid}\"{alternateAddressLabel}}} {cumulativeSum:F3}");

            sb.AppendLine($"{metricName}_duration_milliseconds_count{{agent=\"{AppTelemetryUtils.Agent}\"{bucketLabel},node=\"{nodeAndBucket.Node}\",node_uuid=\"{nodeAndBucket.NodeUuid}\"{alternateAddressLabel}}} {cumulativeCount}");
        }

        exported = sb.ToString();
        return true;
    }
}
