using System;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;

namespace Couchbase.LoadTests.Core.IO.Transcoder
{
    [MemoryDiagnoser]
    public class RawStringTranscoderTests
    {
        private readonly OperationRequestHeader _header = new()
        {
            DataType = DataType.Json,
            Opaque = 55,
            OpCode = OpCode.Get,
            VBucketId = 5
        };

        private OperationBuilder _operationBuilder;
        private RawStringTranscoder _transcoder;
        private string _content;

        [Params(128, 10240, 131072)]
        public int Length { get; set; }

        [GlobalSetup]
        public void BuildSetup()
        {
            _operationBuilder = new OperationBuilder();
            _transcoder = new RawStringTranscoder();
            _content = new string('A', Length);
        }

        [Benchmark(Baseline = true)]
        public ReadOnlyMemory<byte> Encode()
        {
            var builder = _operationBuilder;
            builder.Reset();

            builder.AdvanceToSegment(OperationSegment.Body);
            _transcoder.Encode(builder, _content, new Flags() { TypeCode = TypeCode.String }, OpCode.Set);
            builder.WriteHeader(_header);

            return builder.GetBuffer();
        }
    }
}
