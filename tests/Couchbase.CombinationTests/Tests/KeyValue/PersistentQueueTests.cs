using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.DataStructures;
using Couchbase.KeyValue;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.CombinationTests.Tests.KeyValue;

[Collection(CombinationTestingCollection.Name)]
public class PersistentQueueTests
{
    private readonly CouchbaseFixture _fixture;
    private readonly ITestOutputHelper _outputHelper;

    public PersistentQueueTests(CouchbaseFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task MultiThreadedQueue_DoesNotThrow_CasMismatch()
    {
        var col = await _fixture.GetDefaultCollection();
        var docId = nameof(MultiThreadedQueue_DoesNotThrow_CasMismatch) + Guid.NewGuid();
        var opts = new QueueOptions(CasMismatchRetries: 100);
        var queue = col.Queue<long>(docId, opts);
        await queue.EnqueueAsync(0);

        // make sure the test document doesn't clutter up the bucket forever
        await col.TouchAsync(docId, TimeSpan.FromMinutes(30));

        var tasks = new List<Task>();
        long count = 100; // 1_000 seems to be too much contention;
        for (long i = 0; i < count; i++)
        {
            var val = i;
            var t = Task.Run(async () =>
            {
                await queue.EnqueueAsync(val);
                await Task.Delay(1);
                var dequeued = await queue.DequeueAsync();
                _outputHelper.WriteLine("Enqueued `{0}', Dequeued '{1}'", val, dequeued);
            });

            tasks.Add(t);
        }

        await Task.WhenAll(tasks);
    }
}
