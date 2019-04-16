using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.LoadTests.Helpers;
using Xunit;
using Xunit.Abstractions;
using Magic = Couchbase.Core.IO.Operations.Magic;

namespace Couchbase.LoadTests.Core.IO.Operations
{
    public class OperationReadTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public OperationReadTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task SmallDocuments()
        {
            // Arrange

            const int totalOperations = 10_000_000;
            var maxSimultaneous = Environment.ProcessorCount;

            var converter = new DefaultConverter();
            var transcoder = new DefaultTranscoder(converter, new DefaultSerializer());

            var responses = CreateResponses(converter, transcoder, 1000, 32, 1024);

            // Act

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var startMemory = GC.GetTotalMemory(true);

            await Enumerable.Range(0, totalOperations)
                .ExecuteRateLimited(async i =>
                {
                    var response = responses[i % responses.Count];

                    var operation = new Get<Dictionary<string, object>>
                    {
                        Converter = converter,
                        Transcoder = transcoder
                    };

                    await operation.ReadAsync(new FakeMemoryOwner<byte>(response));
                    operation.GetValue();
                }, maxSimultaneous);

            var finalMemory = GC.GetTotalMemory(false);
            stopwatch.Stop();

            _outputHelper.WriteLine($"Elapsed: {stopwatch.Elapsed.TotalSeconds:N3}s");
            _outputHelper.WriteLine($"Allocated memory: {(double) (finalMemory - startMemory) / (1024 * 1024):N2}Mi");
        }

        [Fact]
        public async Task LargeDocuments()
        {
            // Arrange

            const int totalOperations = 500_000;
            var maxSimultaneous = Environment.ProcessorCount;

            var converter = new DefaultConverter();
            var transcoder = new DefaultTranscoder(converter, new DefaultSerializer());

            var responses = CreateResponses(converter, transcoder, 1000, 65536, 524288);

            // Act

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var startMemory = GC.GetTotalMemory(true);

            await Enumerable.Range(0, totalOperations)
                .ExecuteRateLimited(async i =>
                {
                    var response = responses[i % responses.Count];

                    var operation = new Get<Dictionary<string, object>>
                    {
                        Converter = converter,
                        Transcoder = transcoder
                    };

                    await operation.ReadAsync(new FakeMemoryOwner<byte>(response));
                    operation.GetValue();
                }, maxSimultaneous);

            var finalMemory = GC.GetTotalMemory(false);
            stopwatch.Stop();

            _outputHelper.WriteLine($"Elapsed: {stopwatch.Elapsed.TotalSeconds:N3}s");
            _outputHelper.WriteLine($"Allocated memory: {(double) (finalMemory - startMemory) / (1024 * 1024):N2}Mi");
        }

        #region Helpers

        private List<byte[]> CreateResponses(IByteConverter converter, ITypeTranscoder transcoder, int responseCount, int minSize, int maxSize)
        {
            var docGenerator = new JsonDocumentGenerator(minSize, maxSize);
            var keyGenerator = new GuidKeyGenerator();

            var extras = CreateExtras();

            return docGenerator.GenerateDocumentsWithKeys(keyGenerator, 1000)
                .Select(p =>
                {
                    var body = transcoder.Serializer.Serialize(p.Value);
                    var header = CreateHeader(converter, extras.Length, body.Length);

                    var operation = new byte[header.Length + extras.Length + body.Length];
                    Buffer.BlockCopy(header, 0, operation, 0, header.Length);
                    Buffer.BlockCopy(extras, 0, operation, header.Length, extras.Length);
                    Buffer.BlockCopy(body, 0, operation, header.Length + extras.Length, body.Length);
                    return operation;
                })
                .ToList();
        }

        private byte[] CreateHeader(IByteConverter converter, int extrasLength, int bodyLength)
        {
            var header = new byte[OperationHeader.Length];
            var headerSpan = header.AsSpan();

            converter.FromByte((byte) Magic.Response, headerSpan.Slice(HeaderOffsets.Magic));
            converter.FromInt16((short) ResponseStatus.Success, headerSpan.Slice(HeaderOffsets.Status));
            converter.FromByte((byte) OpCode.Get, headerSpan.Slice(HeaderOffsets.Opcode));
            converter.FromByte((byte) extrasLength, headerSpan.Slice(HeaderOffsets.ExtrasLength));
            converter.FromInt32(bodyLength + extrasLength, headerSpan.Slice(HeaderOffsets.BodyLength));
            converter.FromByte((byte) DataType.Json, headerSpan.Slice(HeaderOffsets.Datatype));

            return header;
        }

        private byte[] CreateExtras()
        {
            var extras = new byte[2];

            extras[0] = (byte) DataFormat.Json & 0xf;
            extras[1] = (byte) TypeCode.Object & 0xff;

            return extras;
        }

        #endregion
    }
}
