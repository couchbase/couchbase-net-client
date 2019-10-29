using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using Xunit;

namespace Couchbase.IntegrationTests.Services.Search
{
    public class SearchTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private const string IndexName = "idx_travel";

        public SearchTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Test_Sync()
        {
            var cluster = _fixture.Cluster;
            var results = cluster.SearchQuery(IndexName,
                new SearchQuery
                {
                    Query = new MatchQuery("inn")
                }.Limit(10).Timeout(TimeSpan.FromMilliseconds(10000))
            );

            //Assert.Equal(SearchStatus.Success, results.Status);
        }

        [Fact]
        public async Task Test_Async()
        {
            var cluster = await _fixture.GetCluster();
            var results = await cluster.SearchQueryAsync(IndexName,
                new SearchQuery
                {
                    Query = new MatchQuery("inn")
                }.Limit(10).Timeout(TimeSpan.FromMilliseconds(10000))
            ).ConfigureAwait(false);

            //Assert.Equal(SearchStatus.Success, results.Status);
        }

        [Fact]
        public void Test_With_HighLightStyle_Html_And_Fields()
        {
            var cluster = _fixture.Cluster;
            var results = cluster.SearchQuery(IndexName,
                new SearchQuery
                {
                    Query = new MatchQuery("inn")
                }.Limit(10).Timeout(TimeSpan.FromMilliseconds(10000)).Highlight(HighLightStyle.Html, "inn")
            );

            //Assert.Equal(SearchStatus.Success, results.Status);
        }

        [Fact]
        public async Task Test_Async_With_HighLightStyle_Html_And_Fields()
        {
            var cluster = await _fixture.GetCluster();
            var results = await cluster.SearchQueryAsync(IndexName,
                new SearchQuery
                {
                    Query = new MatchQuery("inn")
                }.Limit(10).Timeout(TimeSpan.FromMilliseconds(10000)).Highlight(HighLightStyle.Html, "inn")
            ).ConfigureAwait(false);

            //Assert.Equal(SearchStatus.Success, results.Status);
        }

        [Fact]
        public void Facets_Success()
        {
            var cluster = _fixture.Cluster;
            var results = cluster.SearchQuery(IndexName,
                new SearchQuery
                {
                    Query = new MatchQuery("inn")
                }.Facets(
                    new TermFacet("termfacet", "name", 1),
                    new DateRangeFacet("daterangefacet", "thefield", 10).AddRange(DateTime.Now, DateTime.Now.AddDays(1)),
                    new NumericRangeFacet("numericrangefacet", "thefield", 2).AddRange(2.2f, 3.5f)
                )
            );

            //Assert.Equal(SearchStatus.Success, results.Status);
            Assert.Equal(3, results.Facets.Count);
        }

        [Fact]
        public async Task Facets_Async_Success()
        {
            var cluster = await _fixture.GetCluster();
            var results = await cluster.SearchQueryAsync(IndexName,
                new SearchQuery
                {
                    Query = new MatchQuery("inn")
                }.Facets(
                    new TermFacet("termfacet", "name", 1),
                    new DateRangeFacet("daterangefacet", "thefield", 10).AddRange(DateTime.Now, DateTime.Now.AddDays(1)),
                    new NumericRangeFacet("numericrangefacet", "thefield", 2).AddRange(2.2f, 3.5f)
                )
            ).ConfigureAwait(false);

            //Assert.Equal(SearchStatus.Success, results.Status);
            Assert.Equal(3, results.Facets.Count);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
