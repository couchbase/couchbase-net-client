using System;
using System.Collections.Generic;
using Couchbase.Core.Exceptions;
using Couchbase.Search;
using Couchbase.Search.Queries.Compound;
using Couchbase.Search.Queries.Script;
using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.UnitTests.Search
{
    public class CustomScriptQueryTests
    {
        private const string Source = "function f(doc, params) { return doc.score; }";

        public static IEnumerable<object[]> QueryTypes()
        {
            yield return new object[] { "custom_score" };
            yield return new object[] { "custom_filter" };
        }

        // Builds either query type, so each test covers both with the same code.
        private static ISearchQuery Create(string queryType, ISearchQuery inner, string source,
            string[] fields = null, IDictionary<string, object> parameters = null)
        {
            if (queryType == "custom_score")
            {
                var query = new CustomScoreQuery(inner, source);
                if (fields != null) query.Fields(fields);
                if (parameters != null) query.Parameters(parameters);
                return query;
            }
            else
            {
                var query = new CustomFilterQuery(inner, source);
                if (fields != null) query.Fields(fields);
                if (parameters != null) query.Parameters(parameters);
                return query;
            }
        }

        [Theory]
        [MemberData(nameof(QueryTypes))]
        public void Export_WithOnlyQueryAndSource_OmitsFieldsAndParams(string queryType)
        {
            var query = Create(queryType, new MatchAllQuery(), Source);

            var expected = new JObject
            {
                { queryType, new JObject
                {
                    { "query", new JObject { { "match_all", null } } },
                    { "source", Source }
                }}
            };

            Assert.Equal(expected.ToString(Formatting.None), query.Export().ToString(Formatting.None));
        }

        [Theory]
        [MemberData(nameof(QueryTypes))]
        public void Export_WithFieldsAndNestedParams_WritesBoth(string queryType)
        {
            var parameters = new Dictionary<string, object>
            {
                { "factor", 2 },
                { "floor", 0.5 },
                { "name", "michael" },
                { "enabled", true },
                { "missing", null },
                { "tags", new List<object> { "a", 1 } },
                { "boost", new Dictionary<string, object>
                {
                    { "equal", new Dictionary<string, object> { { "name", "michael" } } }
                }}
            };

            var query = Create(queryType, new MatchQuery("michael").Field("name"), Source,
                new[] { "name", "price" }, parameters);

            var expected = JObject.Parse($$"""
                {
                  "{{queryType}}": {
                    "query": { "match": "michael", "prefix_length": 0, "fuzziness": 0, "field": "name" },
                    "source": "{{Source}}",
                    "fields": [ "name", "price" ],
                    "params": {
                      "factor": 2,
                      "floor": 0.5,
                      "name": "michael",
                      "enabled": true,
                      "missing": null,
                      "tags": [ "a", 1 ],
                      "boost": { "equal": { "name": "michael" } }
                    }
                  }
                }
                """);

            Assert.True(JToken.DeepEquals(expected, query.Export()), query.Export().ToString());
        }

        [Theory]
        [MemberData(nameof(QueryTypes))]
        public void Export_WithEmptyFieldsAndParams_OmitsBoth(string queryType)
        {
            var query = Create(queryType, new MatchAllQuery(), Source,
                Array.Empty<string>(), new Dictionary<string, object>());

            var body = (JObject)query.Export()[queryType]!;

            Assert.Null(body["fields"]);
            Assert.Null(body["params"]);
        }

        [Theory]
        [MemberData(nameof(QueryTypes))]
        public void Export_WithCompoundInnerQuery_ExportsItInPlace(string queryType)
        {
            var inner = new ConjunctionQuery(new MatchQuery("michael"), new MatchAllQuery());

            var query = Create(queryType, inner, Source);

            Assert.True(JToken.DeepEquals(inner.Export(), query.Export()[queryType]!["query"]));
        }

        [Theory]
        [MemberData(nameof(QueryTypes))]
        public void Fields_AndParameters_AreCopied(string queryType)
        {
            var fields = new[] { "name" };
            var parameters = new Dictionary<string, object> { { "factor", 2 } };
            var query = Create(queryType, new MatchAllQuery(), Source, fields, parameters);

            fields[0] = "changed";
            parameters["factor"] = 3;
            parameters["added"] = true;

            var body = query.Export()[queryType]!;
            Assert.Equal("name", (string)body["fields"]![0]);
            Assert.Equal(2, (int)body["params"]!["factor"]);
            Assert.Null(body["params"]!["added"]);
        }

        [Theory]
        [MemberData(nameof(QueryTypes))]
        public void Ctor_WhenQueryIsNull_ThrowsArgumentNullException(string queryType)
        {
            Assert.Throws<ArgumentNullException>(() => Create(queryType, null, Source));
        }

        [Theory]
        [MemberData(nameof(QueryTypes))]
        public void Ctor_WhenSourceIsNullOrWhitespace_ThrowsArgumentNullException(string queryType)
        {
            Assert.Throws<ArgumentNullException>(() => Create(queryType, new MatchAllQuery(), null));
            Assert.Throws<ArgumentNullException>(() => Create(queryType, new MatchAllQuery(), " "));
        }

        [Fact]
        public void Fields_WhenNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new CustomScoreQuery(new MatchAllQuery(), Source).Fields(null));
            Assert.Throws<ArgumentNullException>(() => new CustomFilterQuery(new MatchAllQuery(), Source).Fields(null));
        }

        [Fact]
        public void Parameters_WhenNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new CustomScoreQuery(new MatchAllQuery(), Source).Parameters(null));
            Assert.Throws<ArgumentNullException>(() => new CustomFilterQuery(new MatchAllQuery(), Source).Parameters(null));
        }

        // FTS silently ignores a boost on these query types (checked against 8.5.0-1308), so the SDK rejects it.
        [Theory]
        [MemberData(nameof(QueryTypes))]
        public void Boost_ThrowsInvalidArgumentException(string queryType)
        {
            var query = Create(queryType, new MatchAllQuery(), Source);

            Assert.Throws<InvalidArgumentException>(() => query.Boost(2));
        }

        [Fact]
        public void AsReadOnly_ReturnsTheConfiguredValues()
        {
            var inner = new MatchAllQuery();
            var readOnly = new CustomScoreQuery(inner, Source)
                .Fields("name")
                .Parameters(new Dictionary<string, object> { { "factor", 2 } })
                .AsReadOnly();

            Assert.Same(inner, readOnly.Query);
            Assert.Equal(Source, readOnly.Source);
            Assert.Equal(new[] { "name" }, readOnly.Fields);
            Assert.Equal(2, readOnly.Parameters!["factor"]);
        }
    }
}
