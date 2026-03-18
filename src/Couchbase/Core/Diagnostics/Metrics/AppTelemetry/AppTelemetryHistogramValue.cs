using System;
using Couchbase.Core.Compatibility;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

[InterfaceStability(Level.Volatile)]
internal class AppTelemetryHistogramValue(AppTelemetryRequestType requestType)
{
    private AppTelemetryHistogramBins AppTelemetryHistogramBins { get; } = new(requestType);

    public void IncrementCountAndSum(TimeSpan operationLatency)
    {
        AppTelemetryHistogramBins.IncrementCountAndSum(operationLatency);
    }

    public BinSnapshot[] SnapshotAndReset()
    {
        return AppTelemetryHistogramBins.SnapshotAndReset();
    }
}
