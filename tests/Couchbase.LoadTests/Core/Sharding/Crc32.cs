using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Couchbase.LoadTests.Core.Sharding
{
    [SimpleJob(RuntimeMoniker.Net60, baseline: true)]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class Crc32
    {
        private byte[] _buffer;

        [Params(10, 40, 100, 200)]
        public int KeySize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _buffer = Encoding.UTF8.GetBytes(new string('0', KeySize));
        }

        [Benchmark]
        public uint Span()
        {
            return Couchbase.Core.Sharding.Crc32.ComputeHash(_buffer);
        }
    }
}
