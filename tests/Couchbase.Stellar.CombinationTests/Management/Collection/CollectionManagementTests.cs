using System;
using System.Threading.Tasks;
using Couchbase.Management.Collections;
using Couchbase.Stellar.CombinationTests.Fixtures;
using Couchbase.Stellar.CombinationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.Management.Collection;

[Collection(StellarTestCollection.Name)]
public class CollectionManagementTests
{
    private readonly ITestOutputHelper _outputHelper;
    private StellarFixture _fixture;
    private ConsistencyUtils _utils;
    public CollectionManagementTests(StellarFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
        _utils = new ConsistencyUtils(_fixture);
    }

    [Fact]
    public async Task CollectionAlreadyExistsTest()
    {
        var bucket = await _fixture.DefaultBucket();
        var collectionManager = bucket.Collections;

        var collectionName = Guid.NewGuid().ToString()[..6];

        await collectionManager.CreateCollectionAsync("_default", collectionName, new CreateCollectionSettings(TimeSpan.FromSeconds(-1), false));

        var exception = await Record.ExceptionAsync( () => collectionManager.CreateCollectionAsync("_default", collectionName, CreateCollectionSettings.Default));
        Assert.IsType<CollectionExistsException>(exception);
    }
}
