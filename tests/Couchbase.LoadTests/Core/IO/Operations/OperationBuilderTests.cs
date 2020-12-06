using System;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Operations;

namespace Couchbase.LoadTests.Core.IO.Operations
{
    public class OperationBuilderTests
    {
        private readonly byte[] _key = Encoding.UTF8.GetBytes("test_key");
        private readonly byte[] _body = Enumerable.Range(0, 256).Select(p => (byte) p).ToArray();

        private readonly OperationRequestHeader _header = new OperationRequestHeader
        {
            DataType = DataType.Json,
            Opaque = 55,
            OpCode = OpCode.Get,
            VBucketId = 5
        };

        private OperationBuilder _operationBuilder;

        [GlobalSetup(Target = nameof(Build))]
        public void BuildSetup()
        {
            _operationBuilder = new OperationBuilder();
        }

        [Benchmark(Baseline = true)]
        public ReadOnlyMemory<byte> Build()
        {
            var builder = _operationBuilder;
            builder.Reset();

            builder.AdvanceToSegment(OperationSegment.Key);
            builder.Write(_key.AsSpan());
            builder.AdvanceToSegment(OperationSegment.Body);
            builder.Write(_body.AsSpan());
            builder.WriteHeader(_header);

            return builder.GetBuffer();
        }
    }
}
