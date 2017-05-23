using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using Couchbase.Search;
using Newtonsoft.Json;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class FtsDataMapperTests
    {
        [Test]
        public void Success_WhenSuccess_IsTrue()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.js"))
            {
                var result = mapper.Map(stream);

                Assert.IsTrue(result.Success);
            }
        }

        [Test]
        public void Count_WhenSuccess_Returns32()
        {
            var mapper = new SearchDataMapper();
            var fileStream = OpenResource("search-response-success.js");
            using (var stream = fileStream)
            {
                var result = mapper.Map(stream);

                Assert.AreEqual(32, result.Metrics.SuccessCount);
            }
        }

        [Test]
        public void MaxScore_WhenSuccess_ReturnsDouble()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.js"))
            {
                var result = mapper.Map(stream);

                Assert.AreEqual(0.907210290772297, result.Metrics.MaxScore);
            }
        }

        [Test]
        public void Took_WhenSuccess_Returns123165714()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.js"))
            {
                var result = mapper.Map(stream);

                Assert.AreEqual(new TimeSpan(123165714), result.Metrics.Took);
            }
        }

        [Test]
        public void TotalHits_WhenSuccess_Returns116()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.js"))
            {
                var result = mapper.Map(stream);

                Assert.AreEqual(116, result.Metrics.TotalHits);
            }
        }

        [Test]
        public void ErrorCount_WhenSuccess_ReturnsZero()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.js"))
            {
                var result = mapper.Map(stream);

                Assert.AreEqual(0, result.Metrics.ErrorCount);
            }
        }

        [Test]
        public void HitsCount_WhenSuccess_ReturnsPageSize()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.js"))
            {
                var result = mapper.Map(stream);

                Assert.AreEqual(10, result.Hits.Count);
            }
        }

        [Test]
        public void Hits_WhenSuccess_ReturnsValidData()
        {
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-success.js"))
            {
                var result = mapper.Map(stream);

                var first = result.Hits.First();
                Assert.AreEqual("travel_landmark_idx_699e0a42ee02c6b2_27184a97", first.Index);
                Assert.AreEqual("landmark_22563", first.Id);
                Assert.AreEqual(0.907210290772297, first.Score);
               Assert.AreEqual("1.5 miles of the old Plymouth-Tavistock Great Western line, restored by local enthusiasts. Runs a number of old steam engines and other stock, which take visitors up this historic stretch of railway into Plym Woods.", first.Fields.First().Value);
            }
        }

        [Test]
        public void Facets_Are_Populated_When_Result_Contains_Facets()
        {
            ISearchQueryResult result;
            var mapper = new SearchDataMapper();
            using (var stream = OpenResource("search-response-with-facets.js"))
            {
                result = mapper.Map(stream);
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
            });

            Assert.AreEqual(expectedFacets, JsonConvert.SerializeObject(result.Facets));
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

