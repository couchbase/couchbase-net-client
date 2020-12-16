using System;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Converters;

namespace Couchbase.LoadTests.Core.IO.Converters
{
    public class ByteConverterToString
    {
        private byte[] _buffer;

        [Params(10, 100, 500)]
        public int StringLength { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var str = new string('0', StringLength);
            _buffer = System.Text.Encoding.UTF8.GetBytes(str);
        }

        [Benchmark(Baseline = true)]
        public string Current()
        {
            return ByteConverter.ToString(_buffer.AsSpan());
        }
    }
}
