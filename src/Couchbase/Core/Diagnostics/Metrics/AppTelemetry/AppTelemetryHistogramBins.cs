using System;
using System.Threading;
using Couchbase.Core.Compatibility;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

[InterfaceStability(Level.Volatile)]
internal class AppTelemetryHistogramBins
{
    private readonly double[] _boundaries;
    private readonly long[] _counts;
    private readonly long[] _sumsInMicroseconds;

    public AppTelemetryHistogramBins(AppTelemetryRequestType requestType)
    {
        _boundaries = AppTelemetryHistogramUtils.GetPredefinedBucket(requestType);
        _counts = new long[_boundaries.Length];
        _sumsInMicroseconds = new long[_boundaries.Length];
    }

    public void IncrementCountAndSum(TimeSpan operationLatency)
    {
        var opLatencyMs = operationLatency.TotalMilliseconds;
        var opLatencyUs = (long)(opLatencyMs * 1000.0);

        for (int i = 0; i < _boundaries.Length; i++)
        {
            if (opLatencyMs <= _boundaries[i])
            {
                Interlocked.Increment(ref _counts[i]);
                Interlocked.Add(ref _sumsInMicroseconds[i], opLatencyUs);
                return;
            }
        }
    }

    /// <summary>
    /// Atomically snapshots and resets all bins. Returns arrays for export.
    /// </summary>
    public BinSnapshot[] SnapshotAndReset()
    {
        var snapshot = new BinSnapshot[_boundaries.Length];
        for (int i = 0; i < _boundaries.Length; i++)
        {
            var count = Interlocked.Exchange(ref _counts[i], 0);
            var sumUs = Interlocked.Exchange(ref _sumsInMicroseconds[i], 0);
            snapshot[i] = new BinSnapshot(_boundaries[i], (uint)count, sumUs / 1000.0);
        }
        return snapshot;
    }
}
