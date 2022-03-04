using System;
using System.Collections.Generic;
using Couchbase.Core;
using Couchbase.Core.Retry.Search;
using Couchbase.KeyValue;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using Couchbase.Search.Sort;
using Couchbase.Utils;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.UnitTests.Search
{
    public class SearchOptionsTests
    {
        [Fact]
        public void ToJson_JsonStringIsValid()
        {
            var searchOptions = new SearchOptions().Skip(20).Limit(10).Explain(true)
                .Timeout(TimeSpan.FromMilliseconds(10000));

                //var expected = "{\"ctl\":{\"timeout\":10000,\"consistency\":{\"level\":\"at_plus\",\"vectors\":{\"customerIndex\":{\"0\":123,\"1/a0b1c2\":234}}}},\"query\":{\"query\":\"alice smith\",\"boost\": 1},\"size\": 10,\"from\":20,\"highlight\":{\"style\": null,\"fields\":null},\"fields\":[\"*\"],\"facets\":null,\"explain\":true}";
            var expected = "{\"ctl\":{\"timeout\":10000},\"size\":10,\"from\":20,\"explain\":true}";
            var actual = searchOptions.ToJson().ToString(Formatting.None);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToJson_WithFacets()
        {
            var searchOptions = new SearchOptions().Facets(
                new TermFacet("termfacet", "thefield", 10),
                new DateRangeFacet("daterangefacet", "thefield", 10).AddRange(DateTime.Now, DateTime.Now.AddDays(1)),
                new NumericRangeFacet("numericrangefacet", "thefield", 2).AddRange(2.2f, 3.5f));

            Console.WriteLine(searchOptions.ToJson());
        }

        [Fact]
        public void Test_SupportsFields()
        {
            //will not throw ArgumentNullException
            SearchOptions fc = new SearchOptions();
            fc.Highlight(HighLightStyle.Html);
            fc.Fields("name", "style");
        }

        [Fact]
        public void Test_Fields_WhenNull_ThrowsException()
        {
            SearchOptions fc = new SearchOptions();
            fc.Highlight(HighLightStyle.Html);
            Assert.Throws<ArgumentNullException>(() => fc.Fields(null));
        }

        [Fact]
        public void Test_Fields_WhenEmpty_ThrowsException()
        {
            SearchOptions fc = new SearchOptions();
            fc.Highlight(HighLightStyle.Html);
            Assert.Throws<ArgumentNullException>(() => fc.Fields());
        }

        [Fact]
        public void Test_HighLightStyle_Html_And_Fields_Returns_LowerCase()
        {
            var request = new SearchRequest
            {
                Index = "idx_travel",
                Query = new MatchQuery("inn"),
                Options = new SearchOptions().Highlight(HighLightStyle.Html, "inn")
            };

            var result = request.ToJson();
            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new { },
                highlight = new
                {
                    style="html",
                    fields = new [] {"inn"}
                },
                query=new {match="inn", prefix_length=0, fuzziness=0}
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Sort_Adds_FieldNames_To_Output_Json()
        {
            var fields = new List<string> {"name", "-age"};

            var searchOptions = new SearchOptions();
            searchOptions.Sort(fields.ToArray());

            var result = searchOptions.ToJson().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new { },
                sort = fields
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Sort_SearchSort_To_Output_Json()
        {
            var searchSort = new IdSearchSort();

            var searchOptions = new SearchOptions();
            searchOptions.Sort(searchSort);

            var result = searchOptions.ToJson().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new { },
                sort = new[]
                {
                    new
                    {
                        by = "id"
                    }
                }
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Sort_JObject_To_Output_Json()
        {
            var json = new JObject
            {
                new JProperty("foo", "bar")
            };

            var searchOptions = new SearchOptions();
            searchOptions.Sort(json);

            var result = searchOptions.ToJson().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new { },
                sort = new []
                {
                    new
                    {
                        foo = "bar"
                    }
                }
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Raw_To_Output_Json()
        {
            var searchOptions = new SearchOptions();
            searchOptions.Raw("raw1", "abc");
            searchOptions.Raw("raw2", new JObject
            {
                new JProperty("value", 6)
            });

            var result = searchOptions.ToJson().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new {},
                raw1 = "abc",
                raw2 = new
                {
                    value = 6
                }
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void When_DisableScore_True_None_In_Output()
        {
            var searchOptions = new SearchOptions();
            searchOptions.DisableScoring(true);

            var result = searchOptions.ToJson().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new { },
                score = "none"
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void When_DisableScore_False_Nothing_In_Output()
        {
            var searchOptions = new SearchOptions();
            searchOptions.DisableScoring(false);

            var result = searchOptions.ToJson().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new { }
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void When_DisableScore_Default_Nothing_In_Output()
        {
            var searchOptions = new SearchOptions();

            var result = searchOptions.ToJson().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new { }
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Test_IncludeLocations_Not_Set()
        {
            var searchOptions = new SearchOptions();

            var result = searchOptions.ToJson().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new { }
            }, Formatting.None);

            Assert.Equal(expected, result);
        }


        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Test_IncludeLocations_Set(bool includeLocations)
        {
            var searchOptions = new SearchOptions();

            var result = searchOptions.IncludeLocations(includeLocations).ToJson().ToString(Formatting.None);

            var json = new JObject(new JProperty("ctl", new JObject()));
            if (includeLocations)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                json.Add("includeLocations", includeLocations);
            }
            var expected = JsonConvert.SerializeObject(json, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(MatchOperator.Or)]
        [InlineData(MatchOperator.And)]
        public void Test_MatchQuery_MatchOperator(MatchOperator matchOperator)
        {
            var request = new SearchRequest
            {
                Index = "idx_travel",
                Query = new MatchQuery("inn").MatchOperator(matchOperator)
            };

            var result = request.ToJson();

            var json = new JObject(new JProperty("ctl",  new JObject()),
                new JProperty("query",
                    new JObject(new JProperty("match", "inn"), new JProperty("prefix_length", 0),
                        new JProperty("fuzziness", 0), new JProperty("operator", matchOperator.GetDescription()))));

            var expected = JsonConvert.SerializeObject(json, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetFormValues_ScanVector_CorrectValues()
        {
            // Arrange

            var token1 = new MutationToken("defaultIndex", 1, 9, 12);
            var token2 = new MutationToken("defaultIndex", 2, 1, 22);

            var state = new MutationState()
                .Add(
                    // ReSharper disable PossibleUnintendedReferenceComparison
                    Mock.Of<IMutationResult>(m => m.MutationToken == token1),
                    Mock.Of<IMutationResult>(m => m.MutationToken == token2));
                    // ReSharper restore PossibleUnintendedReferenceComparison

            var options = new SearchOptions()
                .ConsistentWith(state);

            // Assert

            var json = options.ToJson("defaultIndex");
            var vectors = json.SelectToken("ctl.consistency.vectors");
            var indexVectors = vectors["defaultIndex"];

            Assert.Equal(12, indexVectors["1/9"]);
            Assert.Equal(22, indexVectors["2/1"]);
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
