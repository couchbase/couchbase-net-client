using System;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.Utils;

namespace Couchbase.LoadTests.Core.Utils
{
    [DisassemblyDiagnoser]
    public class Leb128Tests
    {
        [Params(0u, 256u, 65536u)]
        public uint Value { get; set; }

        readonly byte[] _dest = new byte[8];

        [Benchmark(Baseline = true)]
        public int Baseline()
        {
            return Leb128.Write(_dest, Value);
        }
    }
}
