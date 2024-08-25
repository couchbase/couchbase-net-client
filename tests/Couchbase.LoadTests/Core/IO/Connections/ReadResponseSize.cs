using System.Buffers;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Connections;
using Couchbase.Test.Common.Utils;

namespace Couchbase.LoadTests.Core.IO.Connections
{
    public class ReadResponseSize
    {
        private ReadOnlySequence<byte> _buffer;

        [Params("Contiguous", "Split")]
        public string Type { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            if (Type == "Contiguous")
            {
                // Will not split the header
                _buffer = SequenceHelpers.CreateSequenceWithMaxSegmentSize(new byte[128], 32);
            }
            else
            {
                // Will split the header but not the length
                _buffer = SequenceHelpers.CreateSequenceWithMaxSegmentSize(new byte[128], 8);
            }
        }

        [Benchmark]
        public int Read()
        {
            return MultiplexingConnection.ReadResponseSize(_buffer);
        }
    }
}
