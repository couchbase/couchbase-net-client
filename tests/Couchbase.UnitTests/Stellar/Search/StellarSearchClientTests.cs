#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Search;
using Couchbase.Search.Queries.Compound;
using Couchbase.Search.Queries.Script;
using Couchbase.Search.Queries.Simple;
using Couchbase.Stellar.Search;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.UnitTests.Stellar.Search;

public class StellarSearchClientTests
{
    public static TheoryData<SearchScoring> FusionStrategies() =>
    [
        SearchScoring.ReciprocalRankFusion(),
        SearchScoring.RelativeScoreFusion()
    ];

    [Theory]
    [MemberData(nameof(FusionStrategies))]
    public async Task Throw_FeatureNotAvailableException_When_Score_Fusion_Requested(SearchScoring scoring)
    {
        var client = CreateClient();
        var options = new SearchOptions().Scoring(scoring);

        // The protocol has no score fusion fields, so this fails before any RPC is attempted.
        await Assert.ThrowsAsync<FeatureNotAvailableException>(
            () => client.QueryAsync("indexname", new TermQuery("term"), options));
    }

    [Fact]
    [Obsolete("Covers the deprecated DisableScoring.")]
    public async Task Throw_InvalidArgumentException_When_DisableScoring_And_Scoring_Both_Set()
    {
        var client = CreateClient();
        var options = new SearchOptions().DisableScoring(true).Scoring(SearchScoring.None());

        // Checked before the couchbase2 support check, so it wins even for a mode couchbase2 supports.
        await Assert.ThrowsAsync<InvalidArgumentException>(
            () => client.QueryAsync("indexname", new TermQuery("term"), options));
    }

    public static TheoryData<ISearchQuery> CustomScriptQueries() =>
    [
        new CustomScoreQuery(new MatchAllQuery(), "function f(doc, params) { return doc.score; }"),
        new ConjunctionQuery(new TermQuery("term"),
            new CustomFilterQuery(new MatchAllQuery(), "function f(doc, params) { return true; }"))
    ];

    [Theory]
    [MemberData(nameof(CustomScriptQueries))]
    public async Task Throw_FeatureNotAvailableException_When_Custom_Script_Query_Used(ISearchQuery query)
    {
        var client = CreateClient();

        // The protocol has no custom score or filter query types, so this fails before any RPC is attempted.
        await Assert.ThrowsAsync<FeatureNotAvailableException>(
            () => client.QueryAsync("indexname", query));
    }

    public static TheoryData<ISearchQuery> UnconvertibleQueries() =>
    [
        new UserDefinedQuery(),
        new ConjunctionQuery(new TermQuery("term"), new UserDefinedQuery()),
        new BooleanQuery().Must(new TermQuery("term")).Should(new UserDefinedQuery())
    ];

    [Theory]
    [MemberData(nameof(UnconvertibleQueries))]
    public async Task Throw_FeatureNotAvailableException_When_Query_Type_Cannot_Be_Converted(ISearchQuery query)
    {
        var client = CreateClient();

        // The converter has no protocol mapping for these, so it must fail rather than send an empty query.
        var ex = await Assert.ThrowsAsync<FeatureNotAvailableException>(
            () => client.QueryAsync("indexname", query));

        Assert.Contains(nameof(UserDefinedQuery), ex.Message);
    }

    private sealed class UserDefinedQuery : ISearchQuery
    {
        public ISearchQuery Boost(double boost) => this;

        public JObject Export() => new() { { "term", "term" } };
    }

    private static StellarSearchClient CreateClient() =>
        new(new ClusterTests().CreateClusterFromMocks());
}
#endif
