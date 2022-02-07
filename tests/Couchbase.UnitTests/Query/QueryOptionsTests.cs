using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue;
using Couchbase.Query;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.UnitTests.Query
{
    public class QueryOptionsTests
    {
        [Fact]
        public void Test_ClientContextId_Is_Guid()
        {
            var options = new QueryOptions("SELECT * FROM `Default`").ClientContextId(Guid.NewGuid().ToString());

            //will throw a FormatException if string is not a guid
            Guid.Parse(options.CurrentContextId);
        }

        #region GetFormValues

        [Fact]
        public void GetFormValues_With_PositionParameters()
        {
            var options = new QueryOptions("SELECT * FROM `$1` WHERE name=$2").
                Parameter("default").
                Parameter("bill");

            var values = options.GetFormValues();
            var args = (List<object>)values["args"];

            Assert.Equal("default", args[0]);
            Assert.Equal("bill", args[1]);
        }

        [Fact]
        public void GetFormValues_With_NamedParameters()
        {
            var options = new QueryOptions("SELECT * FROM `$bucket` WHERE name=$name").
                Parameter("bucket","default").
                Parameter("name","bill");

            var values = options.GetFormValues();
            Assert.Equal("default", values["$bucket"]);
            Assert.Equal("bill", values["$name"]);
        }

        [Fact]
        public void GetFormValues_QueryContext_Is_NotNull()
        {
            var options = new QueryOptions("SELECT * FROM WHAT") {QueryContext = "namespace:bucket:scope:collection"};
            var args = options.GetFormValues();

            Assert.Equal("namespace:bucket:scope:collection", args["query_context"]);
        }

        [Fact]
        public void GetFormValues_FlexIndex_When_True_Sends_Parameter()
        {
            var options =  new QueryOptions("SELECT * FROM WHAT").FlexIndex(true);

            var values = options.GetFormValues();
            Assert.Equal(true, actual: values["use_fts"]);
        }

        [Fact]
        public void GetFormValues_FlexIndex_When_False_Sends_Nothing()
        {
            var options = new QueryOptions("SELECT * FROM WHAT").FlexIndex(false);

            var values = options.GetFormValues();
            Assert.False(values.Keys.Contains("use_fts"));
        }

        [Fact]
        public void GetFormValues_FlexIndex_When_Default_Sends_Nothing()
        {
            var options = new QueryOptions("SELECT * FROM WHAT").FlexIndex(false);

            var values = options.GetFormValues();
            Assert.False(values.Keys.Contains("use_fts"));
        }

        [Fact]
        public void GetFormValues_ScanVector_CorrectValues()
        {
            // Arrange

            var token1 = new MutationToken("WHAT", 105, 105, 945678);
            var token2 = new MutationToken("WHAT", 105, 105, 955555);
            var token3 = new MutationToken("WHAT", 210, 210, 12345);

            var state = new MutationState()
                .Add(
                    // ReSharper disable PossibleUnintendedReferenceComparison
                    Mock.Of<IMutationResult>(m => m.MutationToken == token1),
                    Mock.Of<IMutationResult>(m => m.MutationToken == token2),
                    Mock.Of<IMutationResult>(m => m.MutationToken == token3));
                    // ReSharper restore PossibleUnintendedReferenceComparison

            var options = new QueryOptions("SELECT * FROM WHAT")
                .ConsistentWith(state);

            // Assert

            var values = options.GetFormValues();

            var vectors = (Dictionary<string, Dictionary<string, ScanVectorComponent>>) values["scan_vectors"]!;
            var bucketVectors = vectors["WHAT"];

            var vBucketComponent1 = bucketVectors["105"];
            Assert.Equal(955555L, vBucketComponent1.SequenceNumber);
            Assert.Equal(105, vBucketComponent1.VBucketUuid);

            var vBucketComponent2 = bucketVectors["210"];
            Assert.Equal(12345L, vBucketComponent2.SequenceNumber);
            Assert.Equal(210, vBucketComponent2.VBucketUuid);
        }

        #endregion

        #region GetRequestBody

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetRequestBody_With_PositionParameters(bool systemTextJson)
        {
            // Arrange

            var options = new QueryOptions("SELECT * FROM `$1` WHERE name=$2").
                Parameter("default").
                Parameter("bill").
                Parameter(1).
                Parameter(new Poco { Name = "Bob" });

            // Act

            using var content = options.GetRequestBody(GetSerializer(systemTextJson));

            // Assert

            var values = await ExtractValuesAsync(content);
            var args = (JArray)values["args"];

            Assert.Equal("default", args[0]);
            Assert.Equal("bill", args[1]);
            Assert.Equal(1L, args[2]);

            // This confirms our custom serializer was used
            var obj = (JObject) args[3];
            Assert.Equal("Bob", obj.GetValue(systemTextJson ? "name" : "the_name"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetRequestBody_With_NamedParameters(bool systemTextJson)
        {
            // Arrange

            var options = new QueryOptions("SELECT * FROM `$bucket` WHERE name=$name").
                Parameter("bucket","default").
                Parameter("name","bill").
                Parameter("int", 1).
                Parameter("obj", new Poco { Name = "Bob" });

            // Act

            using var content = options.GetRequestBody(GetSerializer(systemTextJson));

            // Assert

            var values = await ExtractValuesAsync(content);
            Assert.Equal("default", values["$bucket"]);
            Assert.Equal("bill", values["$name"]);
            Assert.Equal(1L, values["$int"]);

            // This confirms our custom serializer was used
            var obj = (JObject) values["$obj"];
            Assert.Equal("Bob", obj.GetValue(systemTextJson ? "name" : "the_name"));
        }

        [Fact]
        public async Task GetRequestBody_QueryContext_Is_NotNull()
        {
            // Arrange

            var options = new QueryOptions("SELECT * FROM WHAT") {QueryContext = "namespace:bucket:scope:collection"};

            // Act

            using var content = options.GetRequestBody(GetSerializer());

            // Assert

            var values = await ExtractValuesAsync(content);
            Assert.Equal("namespace:bucket:scope:collection", values["query_context"]);
        }

        [Fact]
        public async Task GetRequestBody_FlexIndex_When_True_Sends_Parameter()
        {
            // Arrange

            var options =  new QueryOptions("SELECT * FROM WHAT").FlexIndex(true);

            // Act

            using var content = options.GetRequestBody(GetSerializer());

            // Assert

            var values = await ExtractValuesAsync(content);
            Assert.Equal(true, actual: values["use_fts"]);
        }

        [Fact]
        public async Task GetRequestBody_FlexIndex_When_False_Sends_Nothing()
        {
            // Arrange

            var options = new QueryOptions("SELECT * FROM WHAT").FlexIndex(false);

            // Act

            using var content = options.GetRequestBody(GetSerializer());

            // Assert

            var values = await ExtractValuesAsync(content);
            Assert.False(values.Keys.Contains("use_fts"));
        }

        [Fact]
        public async Task GetRequestBody_FlexIndex_When_Default_Sends_Nothing()
        {
            // Arrange

            var options = new QueryOptions("SELECT * FROM WHAT").FlexIndex(false);

            // Act

            using var content = options.GetRequestBody(GetSerializer());

            // Assert

            var values = await ExtractValuesAsync(content);
            Assert.False(values.Keys.Contains("use_fts"));
        }

        [Fact]
        public async Task GetRequestBody_Timeout_CorrectFormatting()
        {
            // Arrange

            var options = new QueryOptions("SELECT * FROM WHAT").Timeout(TimeSpan.FromMilliseconds(1000.5));

            // Act

            using var content = options.GetRequestBody(GetSerializer());

            // Assert

            var values = await ExtractValuesAsync(content);
            Assert.Equal("1000ms", values["timeout"]);
        }

        [Fact]
        public async Task GetRequestBody_ScanConsistency_CorrectFormatting()
        {
            // Arrange

            var options = new QueryOptions("SELECT * FROM WHAT").ScanConsistency(QueryScanConsistency.RequestPlus);

            // Act

            using var content = options.GetRequestBody(GetSerializer());

            // Assert

            var values = await ExtractValuesAsync(content);
            Assert.Equal("request_plus", values["scan_consistency"]);
        }

        [Fact]
        public async Task GetRequestBody_ScanVector_CorrectFormatting()
        {
            // Arrange

            var token1 = new MutationToken("WHAT", 105, 105, 945678);
            var token2 = new MutationToken("WHAT", 105, 105, 955555);
            var token3 = new MutationToken("WHAT", 210, 210, 12345);

            var state = new MutationState()
                .Add(
                    // ReSharper disable PossibleUnintendedReferenceComparison
                    Mock.Of<IMutationResult>(m => m.MutationToken == token1),
                    Mock.Of<IMutationResult>(m => m.MutationToken == token2),
                    Mock.Of<IMutationResult>(m => m.MutationToken == token3));
                    // ReSharper restore PossibleUnintendedReferenceComparison

            var options = new QueryOptions("SELECT * FROM WHAT").ConsistentWith(state);

            // Act

            using var content = options.GetRequestBody(GetSerializer());

            // Assert

            var values = await ExtractValuesAsync(content);

            var vectors = (JObject) values["scan_vectors"];
            var bucketVectors = (JObject) vectors["WHAT"];

            var vBucketComponent1 = (JArray) bucketVectors["105"];
            Assert.Equal(955555L, vBucketComponent1[0]);
            Assert.Equal("105", vBucketComponent1[1]);

            var vBucketComponent2 = (JArray) bucketVectors["210"];
            Assert.Equal(12345L, vBucketComponent2[0]);
            Assert.Equal("210", vBucketComponent2[1]);
        }

        #endregion

        [Fact]
        public void Test_CloneIdUsedAlready()
        {
            var cts = new CancellationTokenSource();
            var mutationState = new MutationState();
            mutationState.Add(new MutationResult(0, TimeSpan.FromSeconds(10), new MutationToken("default", 1, 1, 1)));
            var options = new QueryOptions().
                AdHoc(true).AutoExecute(true).
                CancellationToken(cts.Token).
                ClientContextId("clientid").
                ConsistentWith(mutationState).
                FlexIndex(true).
                MaxServerParallelism(1).
                Metrics(true).
                Parameter(1).
                Parameter("name", "value").
                PipelineBatch(1).
                PipelineCap(1).
                PreserveExpiry(true).
                Profile(QueryProfile.Off).
                Raw("foo", "bar").
                Readonly(true).
                ScanCap(1).
                ScanWait(TimeSpan.FromSeconds(10)).
                Timeout(TimeSpan.FromMilliseconds(1)).
                Statement("SELECT 1;").
                ScanCap(1);

            var newOptions = options.CloneIfUsedAlready();
            var newValues = newOptions.GetFormValues();
            var oldValues = options.GetFormValues();

            Assert.Equal(newValues.Count, oldValues.Count);
            Assert.Equal(newValues["max_parallelism"], oldValues["max_parallelism"]);
            Assert.Equal(newValues["statement"], oldValues["statement"]);
            Assert.Equal(newValues["timeout"], oldValues["timeout"]);
            Assert.Equal(newValues["readonly"], oldValues["readonly"]);
            Assert.Equal(newValues["metrics"], oldValues["metrics"]);
            Assert.Equal(newValues["$name"], oldValues["$name"]);
            Assert.Equal(newValues["args"], oldValues["args"]);
            Assert.Equal(newValues["scan_consistency"], oldValues["scan_consistency"]);
            Assert.Equal(newValues["scan_vectors"], oldValues["scan_vectors"]);
            Assert.Equal(newValues["scan_wait"], oldValues["scan_wait"]);
            Assert.Equal(newValues["scan_cap"], oldValues["scan_cap"]);
            Assert.Equal(newValues["pipeline_batch"], oldValues["pipeline_batch"]);
            Assert.Equal(newValues["pipeline_cap"], oldValues["pipeline_cap"]);
            Assert.Equal(newValues["preserve_expiry"], oldValues["preserve_expiry"]);
            Assert.Equal(newValues["foo"], oldValues["foo"]);
            Assert.Equal(newValues["auto_execute"], oldValues["auto_execute"]);
            Assert.Equal(newValues["client_context_id"], oldValues["client_context_id"]);
            Assert.Equal(newValues["use_fts"], oldValues["use_fts"]);
        }

        #region Helpers

        private static ITypeSerializer GetSerializer(bool systemTextJson = false) =>
            systemTextJson
                ? SystemTextJsonSerializer.Create()
                : DefaultSerializer.Instance;

        private static async Task<IDictionary<string, object>> ExtractValuesAsync(HttpContent content)
        {
            var str = await content.ReadAsStringAsync();

            return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(str);
        }

        private class Poco
        {
            // Only alters behavior for Newtonsoft, should be "name" for System.Text.Json
            [JsonProperty("the_name")]
            public string Name { get; set; }
        }

        #endregion
    }
}
