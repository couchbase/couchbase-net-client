using System;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.Utils;

namespace Couchbase.LoadTests.Core.Utils
{
    public class Leb128Tests
    {
        [Params(0u, 256u, 65536u)]
        public uint Value { get; set; }

        [Benchmark(Baseline = true)]
        public int Baseline()
        {
            Span<byte> buffer = stackalloc byte[8];
            return Leb128.Write(buffer, Value);
        }
    }
}
