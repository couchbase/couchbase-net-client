using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Retry;
using Couchbase.Core.Retry.Search;
using Couchbase.KeyValue;
using Couchbase.Search;
using Couchbase.Search.Queries.Compound;
using Couchbase.Search.Queries.Script;
using Couchbase.Search.Queries.Simple;
using Couchbase.Search.Queries.Vector;
using Couchbase.Test.Common.Utils;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Search
{
    public class CustomScriptQueryCapabilityTests
    {
        private const string Source = "function f(doc, params) { return true; }";

        private static ISearchQuery Score() => new CustomScoreQuery(new MatchAllQuery(), Source);
        private static ISearchQuery Filter() => new CustomFilterQuery(new MatchAllQuery(), Source);
        private static ISearchQuery Match() => new MatchQuery("michael");

        private static SearchRequest Prefilter(ISearchQuery filter) => SearchRequest.Create(VectorSearch.Create(
            new VectorQuery("vector_field", new VectorQueryOptions(Filter: filter)).WithVector(new[] { 0.1f })));

        public static TheoryData<string, SearchRequest> RequestsWithCustomScriptQuery() => new()
        {
            { "score", SearchRequest.Create(Score()) },
            { "filter", SearchRequest.Create(Filter()) },
            { "conjunction", SearchRequest.Create(new ConjunctionQuery(Match(), Score())) },
            { "disjunction", SearchRequest.Create(new DisjunctionQuery(Match(), Filter())) },
            { "boolean must", SearchRequest.Create(new BooleanQuery().Must(Score()).Should(Match())) },
            { "boolean should", SearchRequest.Create(new BooleanQuery().Must(Match()).Should(Filter())) },
            { "boolean must not", SearchRequest.Create(new BooleanQuery().Must(Match()).MustNot(Score())) },
            { "nested compound", SearchRequest.Create(new ConjunctionQuery(new DisjunctionQuery(Match(), Filter()))) },
            { "vector prefilter", Prefilter(Filter()) },
            { "vector prefilter compound", Prefilter(new ConjunctionQuery(Match(), Score())) },
            { "query and vector", SearchRequest.Create(Match()).WithVectorSearch(Prefilter(Filter()).VectorSearch!) }
        };

        public static TheoryData<string, SearchRequest> RequestsWithoutCustomScriptQuery() => new()
        {
            { "match", SearchRequest.Create(Match()) },
            { "compound", SearchRequest.Create(new BooleanQuery().Must(Match()).Should(new ConjunctionQuery(Match()))) },
            { "vector", SearchRequest.Create(VectorSearch.Create(new VectorQuery("vector_field").WithVector(new[] { 0.1f }))) },
            { "vector prefilter", Prefilter(Match()) }
        };

        [Theory]
        [MemberData(nameof(RequestsWithCustomScriptQuery))]
        public void ContainsCustomScriptQuery_Finds_It(string name, SearchRequest request)
        {
            Assert.True(request.ContainsCustomScriptQuery(), name);
        }

        [Theory]
        [MemberData(nameof(RequestsWithoutCustomScriptQuery))]
        public void ContainsCustomScriptQuery_When_Absent_Returns_False(string name, SearchRequest request)
        {
            Assert.False(request.ContainsCustomScriptQuery(), name);
        }

        [Theory]
        [MemberData(nameof(RequestsWithCustomScriptQuery))]
        public async Task SearchAsync_Without_Capability_Throws_Before_Sending(string name, SearchRequest request)
        {
            var searchClient = new Mock<ISearchClient>();
            using var cluster = CreateCluster(searchClient.Object, "vectorSearch", "scopedSearchIndex", "scoreFusion");

            var ex = await Assert.ThrowsAsync<FeatureNotAvailableException>(
                () => cluster.SearchAsync("index", request));

            Assert.Contains(ClusterCapabilities.SEARCH_UDF_QUERY, ex.Message);
            searchClient.Verify(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<FtsSearchRequest>(),
                It.IsAny<VectorSearch>(), It.IsAny<IScope>(), It.IsAny<CancellationToken>()), Times.Never, name);
        }

        [Theory]
        [MemberData(nameof(RequestsWithCustomScriptQuery))]
        public async Task SearchAsync_With_Capability_Sends_The_Request(string name, SearchRequest request)
        {
            var searchResult = new Mock<ISearchResult>();
            searchResult.SetupGet(m => m.RetryReason).Returns(RetryReason.NoRetry);
            var searchClient = new Mock<ISearchClient>();
            searchClient
                .Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<FtsSearchRequest>(),
                    It.IsAny<VectorSearch>(), It.IsAny<IScope>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResult.Object);
            using var cluster = CreateCluster(searchClient.Object, "vectorSearch", "scopedSearchIndex", "scoreFusion", "udfQuery");

            var result = await cluster.SearchAsync("index", request);

            Assert.Same(searchResult.Object, result);
            searchClient.Verify(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<FtsSearchRequest>(),
                It.IsAny<VectorSearch>(), It.IsAny<IScope>(), It.IsAny<CancellationToken>()), Times.Once, name);
        }

        [Fact]
        public async Task SearchQueryAsync_Without_Capability_Throws()
        {
            using var cluster = CreateCluster(new Mock<ISearchClient>().Object, "vectorSearch");

            await Assert.ThrowsAsync<FeatureNotAvailableException>(
                () => cluster.SearchQueryAsync("index", new ConjunctionQuery(Match(), Filter())));
        }

        private static Cluster CreateCluster(ISearchClient searchClient, params string[] searchCapabilities)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var options = new ClusterOptions().WithCredentials("u", "p")
                .WithConnectionString("couchbase://localhost");
#pragma warning restore CS0618 // Type or member is obsolete

            var cluster = new Cluster(options)
            {
                LazySearchClient = LazyServiceShimFactory.Create(searchClient)
            };
            cluster.Context.GlobalConfig = new BucketConfig
            {
                ClusterCapabilities = new Dictionary<string, IEnumerable<string>>
                {
                    ["search"] = searchCapabilities
                }
            };
            return cluster;
        }
    }
}
