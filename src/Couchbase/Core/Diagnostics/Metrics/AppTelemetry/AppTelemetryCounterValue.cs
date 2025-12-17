using System.Threading;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

[InterfaceStability(Level.Volatile)]
internal class AppTelemetryCounterValue
{
    private long _canceled = 0;
    private long _timedOut = 0;
    private long _total = 0;

    public long Canceled => _canceled;
    public long TimedOut => _timedOut;
    public long Total => _total + _timedOut + _canceled;

    public AppTelemetryCounterValue()
    {
    }

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
}
