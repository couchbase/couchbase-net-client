using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using Couchbase.Stellar.CombinationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.Search;

[Collection(StellarTestCollection.Name)]
public class StellarSearch
{
    private static ITestOutputHelper _outputHelper;
    private StellarFixture _fixture;
    private const string IndexName = "travel-sample._default.idx-travel";

    //Needs to be run on a cluster with travel-sample loaded, and a generic search index created
    public StellarSearch(StellarFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task TravelSample_Index_Exists()
    {
        var cluster = _fixture.StellarCluster;
        var manager = cluster.SearchIndexes;
        var allIndexes = await manager.GetAllIndexesAsync();
        var names = new HashSet<string>(allIndexes.Select(idx => idx.Name));

        if (!names.Contains(IndexName))
        {
            throw new IndexNotFoundException(
                $"Index {IndexName} not found in test environment.  Available indexes: {string.Join(", ", names)}");
        }
    }

    [Fact]
    public async Task Test_Async()
    {
        var cluster = _fixture.StellarCluster;
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("inn"),
            new SearchOptions().Limit(10).Timeout(TimeSpan.FromMilliseconds(10000)).Scope("_default").Collections("_default")).
            ConfigureAwait(true);

        Assert.True(results.Hits.Count > 0);
    }

    [Fact]
    public async Task Test_Async_With_HighLightStyle_Html_And_Fields()
    {
        var cluster = _fixture.StellarCluster;
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("inn"),
            new SearchOptions().Limit(10).Timeout(TimeSpan.FromMilliseconds(10000))
                .Highlight(HighLightStyle.Html, "inn")
        );

        Assert.True(results.Hits.Count > 0);
    }

    [Fact]
    public async Task Facets_Async_Success()
    {
        var cluster = _fixture.StellarCluster;
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("inn"),
            new SearchOptions().Facets(
                new TermFacet("termfacet", "name", 1),
                new DateRangeFacet("daterangefacet", "thefield", 10).AddRange("testName", DateTime.Now, DateTime.Now.AddDays(1)),
                new NumericRangeFacet("numericrangefacet", "thefield", 2).AddRange("testName", 2.2f, 3.5f)
            )
        );
        foreach (var item in results.Facets)
        {
            _outputHelper.WriteLine(item.Key);
        }
        Assert.Equal(3, results.Facets.Count);
    }

    [Fact]
    public async Task Search_Include_Locations()
    {
        var cluster = _fixture.StellarCluster;
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("inn"),
            new SearchOptions().IncludeLocations(true).Limit(10).Collections("_default", "inventory")
        );
        Assert.NotEmpty(results.Hits[0].Locations);
    }

    [Fact]
    public async Task Search_Match_Operator_Or()
    {
        var cluster = _fixture.StellarCluster;
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("inn hotel").MatchOperator(MatchOperator.Or),
            new SearchOptions().Limit(10)
        );
        Assert.Equal(10,  results.Hits.Count);
    }

    [Fact]
    public async Task Search_Match_Operator_And_Hit()
    {
        //Referring to document "hotel_31944"
        var cluster = _fixture.StellarCluster;
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("http://www.hotelavenuelodge.com Val-d'Isère").MatchOperator(MatchOperator.And),
            new SearchOptions()
        );
        Assert.Equal(1,  results.Hits.Count);
    }

    [Fact]
    public async Task Search_Match_Operator_And_Miss()
    {
        var cluster = _fixture.StellarCluster;
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("http://www.hotelavenuelodge.com asdfg").MatchOperator(MatchOperator.And),
            new SearchOptions()
        );
        Assert.Equal(0,  results.Hits.Count);
    }

}
