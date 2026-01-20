using System;
using System.Threading.Tasks;
using Couchbase.CombinationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Management.Collections;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.CombinationTests.Tests.Management;

[Collection(CombinationTestingCollection.Name)]
public class CollectionManagerTests
{
    private readonly CouchbaseFixture _fixture;
    private readonly ITestOutputHelper _outputHelper;
    private TestHelper _testHelper;
    private volatile bool _isBucketFlushed;

    public CollectionManagerTests(CouchbaseFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
        _testHelper = new TestHelper(fixture);
    }

    [Fact]
    public async Task Test_CreateCollection()
    {
        var bucket = await _fixture.GetDefaultBucket();

        var id = "TestCollection" + Guid.NewGuid();

        var collectionManager = bucket.Collections;
        var settings = new CreateCollectionSettings(TimeSpan.FromSeconds(10));

        var exception = await Record.ExceptionAsync(async () => await collectionManager.CreateCollectionAsync("_default", id, settings));

        Assert.Null(exception);

        await collectionManager.DropCollectionAsync("_default", id);
    }

    [Fact]
    public async Task Test_DropCollection()
    {
        var bucket = await _fixture.GetDefaultBucket();

        var id = "TestCollection" + Guid.NewGuid();

        var collectionManager = bucket.Collections;
        var settings = new CreateCollectionSettings(TimeSpan.FromSeconds(10));

        await collectionManager.CreateCollectionAsync("_default", id, settings);

        var exception = await Record.ExceptionAsync(async () => await collectionManager.DropCollectionAsync("_default", id));

        Assert.Null(exception);
    }

    [CouchbaseVersionDependentFact(MinVersion = "7.2.0")]
    public async Task Test_UpdateCollection_With_History_And_MaxExpiry()
    {
        var bucket = await _fixture.GetDefaultBucket();

        var id = "TestCollection" + Guid.NewGuid();

        var collectionManager = bucket.Collections;
        var createSettings = new CreateCollectionSettings(TimeSpan.FromMinutes(5), false);
        var updateSettings = new UpdateCollectionSettings(TimeSpan.FromMinutes(5), true);

        await collectionManager.CreateCollectionAsync("_default", id, createSettings);

        await _testHelper.WaitUntilCollectionIsPresent(id);

        try
        {
            await collectionManager.UpdateCollectionAsync("_default", id, updateSettings);
            Assert.True(true);
        }
        catch (CouchbaseException e)
        {
            if (e.Context.Message.Contains("Bucket must have storage_mode=magma"))
            {
                _outputHelper.WriteLine("Test failed due to Bucket having a Couchstore backend instead of Magma, but operation went through to the server.");
                Assert.True(true);
            }
            else
            {
                Assert.True(false);
            }
        }

        await collectionManager.DropCollectionAsync("_default", id);
    }
}
