using BenchmarkDotNet.Attributes;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Couchbase.LoadTests.Core.Diagnostics.Metrics
{
    [MemoryDiagnoser]
    public class LoggingMeterRecordValueNoTag
    {
        private LoggingMeter _meter;
        private uint _i;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _meter = new LoggingMeter(new NullLoggerFactory(), new LoggingMeterOptions
            {
                EnabledValue = true,
                ReportingEnabledValue = false
            });

            // The idea here is to at least vaguely emulate real world data by starting at 16us and increasing by 1us
            // every 1024 iterations of the test (right shift 10 bits before passing to RecordValue). This way every RecordValue
            // doesn't get the same exponent when cast to a double, but we do see a distribution of different exponents.
            _i = 16 << 10;
        }

        // This benchmark requests the value record each time for consistency with the behavior
        // of MeterForwarder.

        [Benchmark]
        public void RecordValue()
        {
            var recorder = _meter.ValueRecorder(OuterRequestSpans.ServiceSpan.N1QLQuery);
            recorder.RecordValue(_i++ >>> 10);
        }
    }
}
