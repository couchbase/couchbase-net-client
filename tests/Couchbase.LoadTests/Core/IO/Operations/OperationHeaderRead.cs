using System.Linq;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Operations;

namespace Couchbase.LoadTests.Core.IO.Operations
{
    public class OperationHeaderRead
    {
        private byte[] _buffer;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _buffer = [
                (byte)Magic.AltResponse,
                (byte)OpCode.Get,
                3, // Framing extras length
                0, // Key length
                8, // Extras length
                (byte)DataType.Json,
                0, // Success
                0, // Success,
                ..Enumerable.Repeat((byte)0, 16) // body length, opaque, and CAS
            ];
        }

        [Benchmark]
        public int Read() =>
            OperationHeader.Read(_buffer).BodyOffset;
    }
}
