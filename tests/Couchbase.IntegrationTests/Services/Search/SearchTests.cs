using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Retry.Search;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using Couchbase.Test.Common.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests.Services.Search
{
    public class SearchTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        internal const string IndexName = "idx-travel";

        public SearchTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task TravelSample_Index_Exists()
        {
            var cluster = await _fixture.GetCluster();
            var manager = cluster.SearchIndexes;
            var allIndexes = await manager.GetAllIndexesAsync();
            var names = new HashSet<string>(allIndexes.Select(idx => idx.Name));

            if (!names.Contains(SearchTests.IndexName))
            {
                throw new IndexNotFoundException(
                    $"Index {SearchTests.IndexName} not found in test environment.  Available indexes: {string.Join(", ", names)}");
            }
        }

        [Fact]
        public async Task Test_Async()
        {
            var cluster = await _fixture.GetCluster();
            var results = await cluster.SearchQueryAsync(IndexName,
                new MatchQuery("inn"),
                new SearchOptions().Limit(10).Timeout(TimeSpan.FromMilliseconds(10000)).Scope("_default").Collections("_default")).
                ConfigureAwait(true);

            Assert.True(results.Hits.Count > 0);
        }

        [Fact]
        public async Task Test_Async_With_HighLightStyle_Html_And_Fields()
        {
            var cluster = await _fixture.GetCluster();
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
            var cluster = await _fixture.GetCluster();
            var results = await cluster.SearchQueryAsync(IndexName,
                new MatchQuery("inn"),
                new SearchOptions().Facets(
                    new TermFacet("termfacet", "name", 1),
                    new DateRangeFacet("daterangefacet", "thefield", 10).AddRange("testName", DateTime.Now, DateTime.Now.AddDays(1)),
                    new NumericRangeFacet("numericrangefacet", "thefield", 2).AddRange("testName", 2.2f, 3.5f)
                )
            );
            Assert.Equal(3, results.Facets.Count);
        }

        [Fact]
        public async Task Search_Include_Locations()
        {
            var cluster = await _fixture.GetCluster();
            var results = await cluster.SearchQueryAsync(IndexName,
                new MatchQuery("inn"),
                new SearchOptions().IncludeLocations(true).Limit(10)
            );
            Assert.NotEmpty(results.Hits[0].Locations);
        }

        [Fact]
        public async Task Search_Match_Operator_Or()
        {
            var cluster = await _fixture.GetCluster();
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
            var cluster = await _fixture.GetCluster();
            var results = await cluster.SearchQueryAsync(IndexName,
                new MatchQuery("http://www.hotelavenuelodge.com Val-d'Isère").MatchOperator(MatchOperator.And),
                new SearchOptions()
            );
            Assert.Single(results.Hits);
        }

        [Fact]
        public async Task Search_Match_Operator_And_Miss()
        {
            var cluster = await _fixture.GetCluster();
            var results = await cluster.SearchQueryAsync(IndexName,
                new MatchQuery("http://www.hotelavenuelodge.com asdfg").MatchOperator(MatchOperator.And),
                new SearchOptions()
            );
            Assert.Empty(results.Hits);
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
