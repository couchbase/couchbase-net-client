using BenchmarkDotNet.Attributes;
using Couchbase.Core.Diagnostics.Metrics;

namespace Couchbase.LoadTests.Core.Diagnostics.Metrics
{
    public class HistogramCollectMeasurements
    {
        private HistogramCollector _collector;
        private int[][] _data;
        private int _totalCount;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _collector = new HistogramCollector();

            // The idea here is to at least vaguely emulate real world data by starting at 16us and increasing by 1us
            // every 1024 iterations of the test (right shift 10 bits before passing to RecordValue). This way every RecordValue
            // doesn't get the same exponent when cast to a double, but we do see a distribution of different exponents.
            for (uint i = 16 << 10; i < 16 << 24; i++)
            {
                _collector.AddMeasurement(i >>> 10);
            }

            (_data, _totalCount) = _collector.GetData();
        }

        [Benchmark(Baseline = true)]
        public string Collect()
        {
            _collector.SetData(_data, _totalCount);
            return _collector.CollectMeasurements().ToString();
        }
    }
}
