using System.Threading;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

[InterfaceStability(Level.Volatile)]
internal class AppTelemetryCounterValue
{
    private long _canceled;
    private long _timedOut;
    private long _total;

    public void Increment(AppTelemetryCounterType counterType)
    {
        switch (counterType)
        {
            case AppTelemetryCounterType.TimedOut:
                Interlocked.Increment(ref _timedOut);
                break;
            case AppTelemetryCounterType.Canceled:
                Interlocked.Increment(ref _canceled);
                break;
            case AppTelemetryCounterType.Total:
                Interlocked.Increment(ref _total);
                break;
            default:
                throw new InvalidArgumentException("Unsupported counter type: " + counterType);
        }
    }

    /// <summary>
    /// Atomically snapshots and resets each counter field individually. The three exchanges are
    /// not atomic as a group, a concurrent Increment between exchanges may cause a single export
    /// to have an inconsistent cross-field view. However, no data is lost: the increment
    /// is captured either in the current or next export, and the aggregate across all exports is
    /// always correct.
    /// </summary>
    public (long Canceled, long TimedOut, long Total) SnapshotAndReset()
    {
        var canceled = Interlocked.Exchange(ref _canceled, 0);
        var timedOut = Interlocked.Exchange(ref _timedOut, 0);
        var total = Interlocked.Exchange(ref _total, 0);
        return (canceled, timedOut, total + timedOut + canceled);
    }
}
