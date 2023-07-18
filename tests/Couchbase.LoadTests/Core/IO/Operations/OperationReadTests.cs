using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;

using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.LoadTests.Helpers;
using Magic = Couchbase.Core.IO.Operations.Magic;

namespace Couchbase.LoadTests.Core.IO.Operations
{
    [MemoryDiagnoser]
    public class OperationReadTests
    {
        private ITypeTranscoder _transcoder;
        private byte[] _response;

        [Params(512, 16384, 131072, 524288)]
        public int DocSize;

        [GlobalSetup]
        public void Setup()
        {
            _transcoder = new LegacyTranscoder(new DefaultSerializer());
            _response = CreateResponse(_transcoder, DocSize);
        }

        [Benchmark]
        public void Json()
        {
            using (var operation = new Get<Dictionary<string, object>>
            {
                Transcoder = _transcoder
            })
            {
                operation.Read(new FakeMemoryOwner<byte>(_response));
                operation.GetValue();
            }
        }

        #region Helpers

        private byte[] CreateResponse(ITypeTranscoder transcoder, int size)
        {
            var docGenerator = new JsonDocumentGenerator(size, size);
            var keyGenerator = new GuidKeyGenerator();

            var extras = CreateExtras();

            return docGenerator.GenerateDocumentsWithKeys(keyGenerator, 1)
                .Select(p =>
                {
                    var body = transcoder.Serializer.Serialize(p.Value);
                    var header = CreateHeader(extras.Length, body.Length);

                    var operation = new byte[header.Length + extras.Length + body.Length];
                    Buffer.BlockCopy(header, 0, operation, 0, header.Length);
                    Buffer.BlockCopy(extras, 0, operation, header.Length, extras.Length);
                    Buffer.BlockCopy(body, 0, operation, header.Length + extras.Length, body.Length);
                    return operation;
                })
                .First();
        }

        private byte[] CreateHeader(int extrasLength, int bodyLength)
        {
            var header = new byte[OperationHeader.Length];
            var headerSpan = header.AsSpan();

            headerSpan[HeaderOffsets.Magic] = (byte) Magic.ClientResponse;
            ByteConverter.FromInt16((short) ResponseStatus.Success, headerSpan.Slice(HeaderOffsets.Status));
            headerSpan[HeaderOffsets.Opcode] = (byte) OpCode.Get;
            headerSpan[HeaderOffsets.ExtrasLength] = (byte) extrasLength;
            ByteConverter.FromInt32(bodyLength + extrasLength, headerSpan.Slice(HeaderOffsets.BodyLength));
            headerSpan[HeaderOffsets.Datatype] = (byte) DataType.Json;

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
