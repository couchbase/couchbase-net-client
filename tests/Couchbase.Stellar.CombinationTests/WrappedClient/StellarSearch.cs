using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using Couchbase.Test.Common.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.WrappedClient;

public class StellarSearch
{
    private static ITestOutputHelper _outputHelper;
    private const string IndexName = "idx-travel";

    //Needs to be run on a cluster with travel-sample loaded
    public StellarSearch(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Theory]
    [InlineData("protostellar")]
    [InlineData("couchbase")]
    [InlineData("couchbases")]
    public async Task TravelSample_Index_Exists(string protocol)
    {
        var cluster = await GetCluster(protocol);
        var manager = cluster.SearchIndexes;
        var allIndexes = await manager.GetAllIndexesAsync();
        var names = new HashSet<string>(allIndexes.Select(idx => idx.Name));

        if (!names.Contains(IndexName))
        {
            throw new IndexNotFoundException(
                $"Index {IndexName} not found in test environment.  Available indexes: {string.Join(", ", names)}");
        }
    }

    [Theory]
    [InlineData("protostellar")]
    [InlineData("couchbase")]
    [InlineData("couchbases")]
    public async Task Test_Async(string protocol)
    {
        var cluster = await GetCluster(protocol).ConfigureAwait(false);
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("inn"),
            new SearchOptions().Limit(10).Timeout(TimeSpan.FromMilliseconds(10000)).Scope("_default").Collections("_default")).
            ConfigureAwait(false);

        Assert.True(results.Hits.Count > 0);
    }

    [Theory]
    [InlineData("protostellar")]
    [InlineData("couchbase")]
    [InlineData("couchbases")]
    public async Task Test_Async_With_HighLightStyle_Html_And_Fields(string protocol)
    {
        var cluster = await GetCluster(protocol).ConfigureAwait(false);
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("inn"),
            new SearchOptions().Limit(10).Timeout(TimeSpan.FromMilliseconds(10000))
                .Highlight(HighLightStyle.Html, "inn")
        ).ConfigureAwait(false);

        Assert.True(results.Hits.Count > 0);
    }

    [Theory]
    [InlineData("protostellar")]
    [InlineData("couchbase")]
    [InlineData("couchbases")]
    public async Task Facets_Async_Success(string protocol)
    {
        var cluster = await GetCluster(protocol).ConfigureAwait(false);
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("inn"),
            new SearchOptions().Facets(
                new TermFacet("termfacet", "name", 1),
                new DateRangeFacet("daterangefacet", "thefield", 10).AddRange(DateTime.Now, DateTime.Now.AddDays(1)),
                new NumericRangeFacet("numericrangefacet", "thefield", 2).AddRange(2.2f, 3.5f)
            )
        ).ConfigureAwait(false);
        Assert.Equal(3, results.Facets.Count);
    }

    [Theory]
    [InlineData("protostellar")]
    [InlineData("couchbase")]
    [InlineData("couchbases")]
    public async Task Search_Include_Locations(string protocol)
    {
        var cluster = await GetCluster(protocol).ConfigureAwait(false);
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("inn"),
            new SearchOptions().IncludeLocations(true).Limit(10).Collections("_default", "inventory")
        ).ConfigureAwait(false);
        Assert.NotEmpty(results.Hits[0].Locations);
    }

    [Theory]
    [InlineData("protostellar")]
    [InlineData("couchbase")]
    [InlineData("couchbases")]
    public async Task Search_Match_Operator_Or(string protocol)
    {
        var cluster = await GetCluster(protocol).ConfigureAwait(false);
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("inn hotel").MatchOperator(MatchOperator.Or),
            new SearchOptions().Limit(10)
        ).ConfigureAwait(false);
        Assert.Equal(10,  results.Hits.Count);
    }

    [Theory]
    [InlineData("protostellar")]
    [InlineData("couchbase")]
    [InlineData("couchbases")]
    public async Task Search_Match_Operator_And_Hit(string protocol)
    {
        //Referring to document "hotel_31944"
        var cluster = await GetCluster(protocol).ConfigureAwait(false);
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("http://www.hotelavenuelodge.com Val-d'Isère").MatchOperator(MatchOperator.And),
            new SearchOptions()
        ).ConfigureAwait(false);
        Assert.Equal(1,  results.Hits.Count);
    }

    [Theory]
    [InlineData("protostellar")]
    [InlineData("couchbase")]
    [InlineData("couchbases")]
    public async Task Search_Match_Operator_And_Miss(string protocol)
    {
        var cluster = await GetCluster(protocol).ConfigureAwait(false);
        var results = await cluster.SearchQueryAsync(IndexName,
            new MatchQuery("http://www.hotelavenuelodge.com asdfg").MatchOperator(MatchOperator.And),
            new SearchOptions()
        ).ConfigureAwait(false);
        Assert.Equal(0,  results.Hits.Count);
    }

    public static async Task<ICluster> GetCluster(string protocol)
    {
        var opts = new ClusterOptions()
        {
            UserName = "Administrator",
            Password = "password"
        };

        var loggerFactory = new TestOutputLoggerFactory(_outputHelper);
        opts.WithLogging(loggerFactory);

        var connectionString = $"{protocol}://localhost";
        if (connectionString.Contains("//localhost"))
        {
            opts.KvIgnoreRemoteCertificateNameMismatch = true;
            opts.HttpIgnoreRemoteCertificateMismatch = true;
        }

        return await StellarCluster.ConnectAsync(connectionString, opts);
    }

}
