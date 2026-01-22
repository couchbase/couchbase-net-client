using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Transcoders;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Test.Common.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests.Core.IO.Connections.DataFlow
{
    public class DataFlowConnectionPoolTests
    {
        public DataFlowConnectionPoolTests()
        {
        }

        [Fact]
        public async Task TestScaleDown()
        {
            await using var fixture = new ClusterFixture(options =>
            {
                options.NumKvConnections = 1;
                options.MaxKvConnections = 2;
#pragma warning disable CS0618 // Type or member is obsolete
                options.Experiments.ChannelConnectionPools = false;
#pragma warning restore CS0618 // Type or member is obsolete
            });
            await fixture.InitializeAsync();

            var bucket = await fixture.GetDefaultBucket();
            var nodes = ((CouchbaseBucket)bucket).Nodes.OfType<ClusterNode>().ToArray();
            Assert.Single(nodes);

            var connectionPools = nodes.Select(p => p.ConnectionPool).ToArray();

            // Scale up to two connections
            await Task.WhenAll(connectionPools.Select(p => p.ScaleAsync(1)));
            Assert.All(connectionPools, p => Assert.Equal(2, p.Size));

            // We'll write 10KB for each operation so that we have some real load on the send side
            var transcoder = new RawBinaryTranscoder();
            var fakeBytes = new byte[10 * 1024];

            var collection = bucket.DefaultCollection();
            using var limitedParallelization = new SemaphoreSlim(100);
            // Run a bunch of get operations, but make sure we don't start too many at once.
            // We don't want timeouts just because we flood the connections
            var operations = Enumerable.Range(0, 20000 * connectionPools.Length).Select(async _ =>
            {
                await limitedParallelization.WaitAsync();
                try
                {
                    var key = Guid.NewGuid().ToString();
                    await collection.UpsertAsync(key, fakeBytes, new KeyValue.UpsertOptions().Transcoder(transcoder));
                    await collection.RemoveAsync(key);
                }
                finally
                {
                    limitedParallelization.Release();
                }
            }).ToList();

            // Give some time for the tasks to get cranking
            await Task.Delay(500);

            // Scale back down to one connection
            await Task.WhenAll(connectionPools.Select(p => p.ScaleAsync(-1)));
            Assert.All(connectionPools, p => Assert.Equal(1, p.Size));

            // Wait for the operations to all complete. Some of them should fail with timeouts if they get dropped during scale down.
            await Task.WhenAll(operations);
        }
    }
}
