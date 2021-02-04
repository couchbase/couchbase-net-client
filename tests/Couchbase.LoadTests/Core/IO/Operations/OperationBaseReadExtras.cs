using System;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Operations;

namespace Couchbase.LoadTests.Core.IO.Operations
{
    public class OperationBaseReadExtras
    {
        private readonly FakeOperation _operation = new FakeOperation();

        // upper byte 0 is DataFormat = Json, Compression = None
        // Bytes 2-3 is TypeCode = Object
        // Bytes 4-7 are Expiration = 0
        private readonly byte[] _extras = {0x2, 0x0, 0x0, 0x1, 0x0, 0x0, 0x0, 0x0};

        [Benchmark(Baseline = true)]
        public void Current()
        {
            _operation.ReadExtrasPublic(_extras.AsSpan());
        }

        private class FakeOperation : MutationOperationBase<string>
        {
            public override OpCode OpCode => OpCode.Set;

            public FakeOperation() : base("bucket", "key")
            {
            }

            public void ReadExtrasPublic(ReadOnlySpan<byte> data) => ReadExtras(data);
        }
    }
}
