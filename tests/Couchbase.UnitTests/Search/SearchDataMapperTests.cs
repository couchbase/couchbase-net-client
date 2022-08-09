using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Search;
using Couchbase.UnitTests.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Couchbase.UnitTests.Search
{
    public class FtsDataMapperTests
    {
        [Fact]
        public async Task Success_Is_False_When_Errors_Returned()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("error-with-errors.json"))
            {
                var result = await mapper.MapAsync(stream).ConfigureAwait(false);
                Assert.Equal(6, result.MetaData.ErrorCount);
            }
        }

        [Fact]
        public async Task Success_WhenSuccess_IsTrue()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.json"))
            {
                var result = await mapper.MapAsync(stream).ConfigureAwait(false);
                Assert.NotEqual(0, result.MetaData.SuccessCount);
                Assert.Equal(result.MetaData.TotalCount, result.MetaData.SuccessCount);
                //Assert.True(result.Success);
            }
        }

        [Fact]
        public async Task Count_WhenSuccess_Returns32()
        {
            var mapper = new SearchDataMapper();
            var fileStream = OpenResource("search-response-success.json");
            using (var stream = fileStream)
            {
                var result = await mapper.MapAsync(stream).ConfigureAwait(false);

                Assert.Equal(32, result.MetaData.SuccessCount);
            }
        }

        [Fact]
        public async Task MaxScore_WhenSuccess_ReturnsDouble()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.json"))
            {
                var result = await mapper.MapAsync(stream).ConfigureAwait(false);

                Assert.Equal(0.907210290772297, result.MetaData.MaxScore);
            }
        }

        [Fact]
        public async Task Took_WhenSuccess_Returns123165714()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.json"))
            {
                var result = await mapper.MapAsync(stream).ConfigureAwait(false);

                Assert.Equal(123, result.MetaData.TimeTook.Milliseconds);
                Assert.Equal(1231657, result.MetaData.TimeTook.Ticks); // <- max resolution of TimeStamp, currently
            }
        }

        [Fact]
        public async Task TotalHits_WhenSuccess_Returns116()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.json"))
            {
                var result = await mapper.MapAsync(stream).ConfigureAwait(false);

                Assert.Equal(116, result.MetaData.TotalHits);
            }
        }

        [Fact]
        public async Task ErrorCount_WhenSuccess_ReturnsZero()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.json"))
            {
                var result = await mapper.MapAsync(stream).ConfigureAwait(false);

                Assert.Equal(0, result.MetaData.ErrorCount);
            }
        }

        [Fact]
        public async Task HitsCount_WhenSuccess_ReturnsPageSize()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.json"))
            {
                var result = await mapper.MapAsync(stream).ConfigureAwait(false);

                Assert.Equal(10, result.Hits.Count);
            }
        }

        [Fact]
        public async Task Hits_WhenSuccess_ReturnsValidData()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.json"))
            {
                var result = await mapper.MapAsync(stream).ConfigureAwait(false);

                var first = result.Hits.First();
                Assert.Equal("travel_landmark_idx_699e0a42ee02c6b2_27184a97", first.Index);
                Assert.Equal("landmark_22563", first.Id);
                Assert.Equal(0.907210290772297, first.Score);
               Assert.Equal("1.5 miles of the old Plymouth-Tavistock Great Western line, restored by local enthusiasts. Runs a number of old steam engines and other stock, which take visitors up this historic stretch of railway into Plym Woods.", first.Fields.First().Value);
            }
        }

        [Fact]
        public async Task Facets_Are_Populated_When_Result_Contains_Facets()
        {
            ISearchResult result;
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-with-facets.json"))
            {
                result = await mapper.MapAsync(stream).ConfigureAwait(false);
            }

            var expectedFacets = JsonConvert.SerializeObject(new
            {
                category = new TermFacetResult
                {
                    Name = "category",
                    Field = "term_field",
                    Total = 100,
                    Missing = 65,
                    Other = 35,
                    Terms = new[]
                    {
                        new Term {Name = "term1", Count = 10}
                    }
                },
                strength = new NumericRangeFacetResult
                {
                    Name = "strength",
                    Field = "numeric_field",
                    Total = 13,
                    Missing = 11,
                    Other = 2,
                    NumericRanges = new[]
                    {
                        new NumericRange {Name = "numeric1", Min = 0.1f, Max = 0.2f, Count = 50}
                    }
                },
                updateRange = new DateRangeFacetResult
                {
                    Name = "updateRange",
                    Field = "daterange_field",
                    Total = 65,
                    Missing = 43,
                    Other = 22,
                    DateRanges = new[]
                    {
                        new DateRange
                        {
                            Name = "daterange1",
                            Start = new DateTime(2017, 1, 1),
                            End = new DateTime(2017, 1, 2),
                            Count = 54
                        }
                    }
                }
            }, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            Assert.Equal(expectedFacets, JsonConvert.SerializeObject(result.Facets, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }));
        }

        private Stream OpenResource(string resourceName)
        {
            return ResourceHelper.ReadResourceAsStream(resourceName);
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
