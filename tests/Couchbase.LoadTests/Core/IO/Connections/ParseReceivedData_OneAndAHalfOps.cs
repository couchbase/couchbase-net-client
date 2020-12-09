using System;
using System.IO;
using System.Net;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Converters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Couchbase.LoadTests.Core.IO.Connections
{
    // ReSharper disable once InconsistentNaming
    public class ParseReceivedData_OneAndAHalfOps
    {
        private MultiplexingConnection _multiplexingConnection;

        [Params(0, 1024)]
        public int BodySize { get; set; }

        private byte[] _buffer;

        private void SetupOps()
        {
            var singleOp = new byte[BodySize + HeaderOffsets.HeaderLength];
            ByteConverter.FromInt32(BodySize, singleOp.AsSpan(HeaderOffsets.BodyLength));

            _buffer = new byte[singleOp.Length * 6 / 4];
            singleOp.AsSpan().CopyTo(_buffer.AsSpan());
            singleOp.AsSpan(0, singleOp.Length / 2).CopyTo(_buffer.AsSpan(singleOp.Length));
        }

        [GlobalSetup(Target = nameof(Current))]
        public void Setup()
        {
            _multiplexingConnection = new MultiplexingConnection(new MemoryStream(), new IPEndPoint(0, 0),
                new IPEndPoint(0, 0), NullLogger<MultiplexingConnection>.Instance);

            SetupOps();
        }

        [Benchmark(Baseline = true)]
        public void Current()
        {
            int length = _buffer.Length;
            _multiplexingConnection.ParseReceivedData(_buffer, ref length);
        }
    }
}
