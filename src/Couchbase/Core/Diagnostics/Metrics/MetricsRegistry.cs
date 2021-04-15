using App.Metrics;
using App.Metrics.ReservoirSampling.SlidingWindow;
using App.Metrics.Timer;

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// A registry for metrics definitions
    /// </summary>
    public static class MetricsRegistry
    {
        public static TimerOptions KvTimerHistogram => new()
        {
            Name = "Request Timer",
            MeasurementUnit = Unit.Requests,
            DurationUnit = TimeUnit.Microseconds,
            RateUnit = TimeUnit.Microseconds,
            Reservoir = () => new DefaultSlidingWindowReservoir(1048),
        };
    }
}
