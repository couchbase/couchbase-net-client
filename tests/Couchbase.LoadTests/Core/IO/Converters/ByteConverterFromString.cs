using System;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Converters;

namespace Couchbase.LoadTests.Core.IO.Converters
{
    public class ByteConverterFromString
    {
        private readonly byte[] _buffer = new byte[1024];
        private string _string;

        [Params(10, 100, 500)]
        public int StringLength { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _string = new string('0', StringLength);
        }

        [Benchmark(Baseline = true)]
        public int Current()
        {
            return ByteConverter.FromString(_string, _buffer.AsSpan());
        }
    }
}
