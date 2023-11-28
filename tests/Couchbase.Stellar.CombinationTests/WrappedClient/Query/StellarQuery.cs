using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.WrappedClient.Query;

[Collection(StellarTestCollection.Name)]
public class StellarQuery
{
    private readonly ITestOutputHelper _outputHelper;
    private StellarFixture _fixture;

    public StellarQuery(StellarFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task ClusterQuery()
    {
        var cluster = _fixture.StellarCluster;
        var collection = await _fixture.DefaultCollection().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString();
        var statement = $"SELECT RAW \"{id}\" FROM `default`.`_default`.`_default` LIMIT 1";
        await collection.UpsertAsync(id, new { Content = "content" }).ConfigureAwait(false);
        var queryResult = await cluster.QueryAsync<string>(statement);
        await foreach (var row in queryResult.Rows)
        {
            Assert.Equal(row, id);
        }

        await collection.RemoveAsync(id).ConfigureAwait(false);
    }

    [Fact]
    public async Task ScopedQuery()
    {
        var cluster = _fixture.StellarCluster;
        var bucket1 = await cluster.BucketAsync("default");
        var scope = bucket1.DefaultScope();
        var collection = await _fixture.DefaultCollection().ConfigureAwait(false);
        var id = Guid.NewGuid().ToString();
        var statement = $"SELECT RAW \"{id}\" FROM `default`.`_default`.`_default` LIMIT 1";
        await collection.UpsertAsync(id, new { Content = "content" }).ConfigureAwait(false);
        var queryResult = await scope.QueryAsync<string>(statement);
        await foreach (var row in queryResult.Rows)
        {
            Assert.Equal(row, id);
        }

        await collection.RemoveAsync(id).ConfigureAwait(false);
    }
}
