using System.Collections.Generic;
using System.Net;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Sharding;
using Microsoft.Extensions.Logging.Abstractions;

namespace Couchbase.LoadTests.Core.Sharding
{
    [MemoryDiagnoser]
    // ReSharper disable once InconsistentNaming
    public class VBucketKeyMapper_GetIndex
    {
        private string _key;
        private VBucketKeyMapper _keyMapper;

        [Params(10, 40, 100)]
        public int KeySize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _key = new string('0', KeySize);
            _keyMapper = new VBucketKeyMapper(
                new BucketConfig(),
                new VBucketServerMap(new VBucketServerMapDto()),
                new VBucketFactory(new NullLogger<VBucket>()));
        }

        [Benchmark(Baseline = true)]
        public short Current()
        {
            return _keyMapper.GetIndex(_key);
        }
    }
}
