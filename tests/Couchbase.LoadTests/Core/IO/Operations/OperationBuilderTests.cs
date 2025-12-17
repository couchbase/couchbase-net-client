using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Utils;
using Couchbase.LoadTests.Helpers;

namespace Couchbase.LoadTests.Core.IO.Operations
{
    [MemoryDiagnoser]
    public partial class OperationBuilderTests
    {
        private const string Key = "test_key";

        private readonly OperationRequestHeader _header = new()
        {
            DataType = DataType.Json,
            Opaque = 55,
            OpCode = OpCode.Get,
            VBucketId = 5
        };

        private OperationBuilder _operationBuilder;
        private SystemTextJsonSerializer _serializer;
        private Dictionary<string, string> _body;

        [Params(128, 10240, 131072)]
        public int DocSize { get; set; }

        [GlobalSetup]
        public void BuildSetup()
        {
            _operationBuilder = new OperationBuilder();
            _serializer = SystemTextJsonSerializer.Create(JsonContext.Default);
            _body = JsonDocumentGenerator.GenerateDocument(DocSize);
        }

        [Benchmark(Baseline = true)]
        public ReadOnlyMemory<byte> Build()
        {
            var builder = _operationBuilder;
            builder.Reset();

            builder.AdvanceToSegment(OperationSegment.Key);
            WriteKey(builder);
            builder.AdvanceToSegment(OperationSegment.Body);
            _serializer.Serialize((IBufferWriter<byte>) builder, _body);
            builder.WriteHeader(_header);

            return builder.GetBuffer();
        }

        private static void WriteKey(OperationBuilder builder)
        {
            var buffer = builder.GetSpan(OperationHeader.MaxKeyLength + Leb128.MaxLength + sizeof(ushort) * 2);
            var keyLength = System.Text.Encoding.UTF8.GetBytes(Key, buffer);
            builder.Advance(keyLength);
        }

        [JsonSerializable(typeof(Dictionary<string, string>))]
        private partial class JsonContext : JsonSerializerContext
        {
        }
    }
}
