using System;
using BenchmarkDotNet.Attributes;
using Couchbase.Utils;

namespace Couchbase.LoadTests.Utils
{
    public class ToTtl
    {
        private TimeSpan _expireTimeSpan;

        [Params(1.0, 31.0)]
        public double ExpireDays
        {
            get => _expireTimeSpan.TotalDays;
            set => _expireTimeSpan = TimeSpan.FromDays(value);
        }

        [Benchmark(Baseline = true)]
        public uint Current() =>
            _expireTimeSpan.ToTtl();
    }
}
