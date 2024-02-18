using System;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.Sharding;

namespace Couchbase.LoadTests.Core.Sharding
{
    [MemoryDiagnoser]
    public class KetamaKeyMapperTests
    {
        private readonly string _key = Guid.NewGuid().ToString();
        private KetamaKeyMapper _keyMapper;

        [GlobalSetup]
        public void Setup()
        {
            _keyMapper = new KetamaKeyMapper([
                new HostEndpointWithPort("1", 11210),
                new HostEndpointWithPort("2", 11210),
                new HostEndpointWithPort("3", 11210)
            ]);
        }

        [Benchmark(Baseline = true)]
        public object Current()
        {
            return _keyMapper.MapKey(_key);
        }
    }
}
