using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Couchbase.Core.Compatibility;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

[InterfaceStability(Level.Volatile)]
internal class AppTelemetryHistogramValue
{
    public AppTelemetryHistogramBins AppTelemetryHistogramBins { get; }

    public AppTelemetryHistogramValue(AppTelemetryRequestType requestType)
    {
        AppTelemetryHistogramBins = new AppTelemetryHistogramBins(requestType);
    }

    public void IncrementCountAndSum(TimeSpan operationLatency)
    {
        AppTelemetryHistogramBins.IncrementCountAndSum(operationLatency);
    }
}
