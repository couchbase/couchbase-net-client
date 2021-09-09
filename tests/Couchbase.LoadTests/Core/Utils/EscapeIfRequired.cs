using System;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.Utils;

namespace Couchbase.LoadTests.Core.Utils
{
    [MemoryDiagnoser]
    public class EscapeIfRequired
    {
        [Params("default", "`beer-sample`")]
        public string Input { get; set; }

        [Benchmark(Baseline = true)]
        public string Original()
        {
            return Input.EscapeIfRequired();
        }
    }
}
