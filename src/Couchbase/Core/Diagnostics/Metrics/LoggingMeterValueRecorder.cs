#nullable enable
using System.Collections.Generic;
using App.Metrics;

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// A <see cref="IValueRecorder"/> implementation for collecting latency metrics for a
    /// Couchbase service and conjunction with <see cref="LoggingMeter"/>.
    /// </summary>
    internal class LoggingMeterValueRecorder : IValueRecorder
    {
        private readonly IMetricsRoot? _metrics;

        public LoggingMeterValueRecorder(IMetricsRoot? metrics)
        {
            _metrics = metrics;
        }

        /// <inheritdoc />
        public void RecordValue(uint value, KeyValuePair<string, string>? tag = null)
        {
            if (tag == null)
                _metrics?.Measure.Timer.Time(MetricsRegistry.KvTimerHistogram, value);
            else
                _metrics?.Measure.Timer.Time(MetricsRegistry.KvTimerHistogram, new MetricTags(tag?.Key, tag?.Value),
                    value);
        }
    }
}
