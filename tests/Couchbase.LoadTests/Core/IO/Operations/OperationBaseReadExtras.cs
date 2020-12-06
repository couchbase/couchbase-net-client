using System;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Operations;

namespace Couchbase.LoadTests.Core.IO.Operations
{
    public class OperationBaseReadExtras
    {
        private readonly Set<string> _operation = new Set<string>("bucket", "key");

        // upper byte 0 is DataFormat = Json, Compression = None
        // Bytes 2-3 is TypeCode = Object
        // Bytes 4-7 are Expiration = 0
        private readonly byte[] _extras = {0x2, 0x0, 0x0, 0x1, 0x0, 0x0, 0x0, 0x0};

        [Benchmark(Baseline = true)]
        public void Current()
        {
            _operation.ReadExtras(_extras.AsSpan());
        }
    }
}
