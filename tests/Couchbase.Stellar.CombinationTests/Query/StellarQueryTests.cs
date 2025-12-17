using System;
using System.Threading.Tasks;
using Couchbase.Stellar.CombinationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.Query;

[Collection(StellarTestCollection.Name)]
public class StellarQueryTests
{
    private readonly ITestOutputHelper _outputHelper;
    private StellarFixture _fixture;

    public StellarQueryTests(StellarFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task ClusterQuery()
    {
        var cluster = _fixture.StellarCluster;
        var collection = await _fixture.DefaultCollection().ConfigureAwait(true);
        var id = Guid.NewGuid().ToString();
        var statement = $"SELECT RAW \"{id}\" FROM `default`.`_default`.`_default` LIMIT 1";
        await collection.UpsertAsync(id, new { Content = "content" }).ConfigureAwait(true);
        var queryResult = await cluster.QueryAsync<string>(statement);
        await foreach (var row in queryResult.Rows)
        {
            Assert.Equal(row, id);
        }

        await collection.RemoveAsync(id).ConfigureAwait(true);
    }

    [Fact]
    public async Task ScopedQuery()
    {
        var cluster = _fixture.StellarCluster;
        var bucket1 = await cluster.BucketAsync("default");
        var scope = bucket1.DefaultScope();
        var collection = await _fixture.DefaultCollection().ConfigureAwait(true);
        var id = Guid.NewGuid().ToString();
        var statement = $"SELECT RAW \"{id}\" FROM `default`.`_default`.`_default` LIMIT 1";
        await collection.UpsertAsync(id, new { Content = "content" }).ConfigureAwait(true);
        var queryResult = await scope.QueryAsync<string>(statement);
        await foreach (var row in queryResult.Rows)
        {
            Assert.Equal(row, id);
        }

        await collection.RemoveAsync(id).ConfigureAwait(true);
    }
}
