#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using Couchbase.Stellar.Search;
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

    private static StellarSearchClient CreateClient() =>
        new(new ClusterTests().CreateClusterFromMocks());
}
#endif
