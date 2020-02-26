using System;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Couchbase.KeyValue;

namespace Couchbase.LoadTests.Core.IO.Connections
{
    [SimpleJob(RunStrategy.Monitoring)]
    [MemoryDiagnoser]
    public class MultiplexingConnectionTests
    {
        private ICluster _cluster;
        private ICouchbaseCollection _collection;
        private string _key;

        [GlobalSetup]
        public async Task GlobalSetup()
        {
            var options = new ClusterOptions()
                .WithConnectionString("couchbase://localhost")
                .WithCredentials("Administrator", "password");

            _cluster = await Cluster.ConnectAsync(options);

            var bucket = await _cluster.BucketAsync("default");
            _collection = bucket.DefaultCollection();

            _key = Guid.NewGuid().ToString();

            await _collection.InsertAsync(_key, new {name = "mike"}).ConfigureAwait(false);
        }

        [GlobalCleanup]
        public async Task GlobalCleanup()
        {
            await _collection.RemoveAsync(_key).ConfigureAwait(false);

            _cluster.Dispose();
        }

        [Benchmark]
        public async Task ParallelOperations()
        {
            async Task DoOneHundredGets()
            {
                for (var i = 0; i < 100; i++)
                {
                    using var result = await _collection.GetAsync(_key).ConfigureAwait(false);
                }
            }

            var parallelTasks = Enumerable.Range(1, 8)
                .Select(_ => DoOneHundredGets())
                .ToList();

            await Task.WhenAll(parallelTasks).ConfigureAwait(false);
        }
    }
}
