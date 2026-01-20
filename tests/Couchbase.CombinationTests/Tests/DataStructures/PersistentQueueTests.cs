using System;
using System.Threading.Tasks;
using Couchbase.CombinationTests.Fixtures;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.CombinationTests.Tests.DataStructures;


[Collection(CombinationTestingCollection.Name)]
public class PersistentQueueTests
{
    private readonly CouchbaseFixture _fixture;
    private readonly ITestOutputHelper _outputHelper;
    private TestHelper _testHelper;

    public PersistentQueueTests(CouchbaseFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
        _testHelper = new TestHelper(fixture);
    }


    [Fact]
    async Task Test_PersistentQueue_Enqueues_And_Dequeues_In_Correct_FIFO_Order()
    {
        var collection = await _fixture.GetDefaultCollection();

        var documentId = "QueueTest" + Guid.NewGuid();
        var queue = collection.Queue<int>(documentId);

        for (var queueItem = 1; queueItem <= 5; queueItem++)
        {
            _outputHelper.WriteLine($"Enqueing {queueItem}");
            await queue.EnqueueAsync(queueItem);
        }

        var count = 1;
        while (true)
        {
            int currentPeek;
            try
            {
                currentPeek = await queue.PeekAsync();
            }
            catch (PathNotFoundException)
            {
                break;
            }

            Assert.Equal(count, currentPeek);
            _outputHelper.WriteLine($"Peeking at {currentPeek} before dequeing");
            await queue.DequeueAsync();
            count++;

        }
    }
}
