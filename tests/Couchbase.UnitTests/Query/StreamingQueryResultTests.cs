using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Serializers.SystemTextJson;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Xunit;

namespace Couchbase.UnitTests.Query
{
    public class StreamingQueryResultTests
    {
        #region retry handling

        [Theory]
        [InlineData(@"Documents\Query\Retrys\4040.json", true, true)]
        [InlineData(@"Documents\Query\Retrys\4050.json", true, false)]
        [InlineData(@"Documents\Query\Retrys\4070.json", true, false)]
        [InlineData(@"Documents\Query\Retrys\5000.json", true, false)]
        [InlineData(@"Documents\Query\Retrys\4040.json", false, false)]
        [InlineData(@"Documents\Query\Retrys\4050.json", false, true)]
        [InlineData(@"Documents\Query\Retrys\4070.json", false, true)]
        [InlineData(@"Documents\Query\Retrys\5000.json", false, true)]
        public async Task ShouldRetry_Handles_Retry_Cases(string fileName, bool enableEnhancedPreparedStatements, bool shouldRetry)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(fileName);

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);
            await streamingResult.InitializeAsync();

            // Act

            var actual = streamingResult.ShouldRetry(enableEnhancedPreparedStatements);

            // Assert

            Assert.Equal(shouldRetry, actual);
        }

        #endregion

        #region InitializeAsync

        [Theory]
        [InlineData(@"Documents\Query\query-n1ql-error-response-400.json", QueryStatus.Fatal)]
        [InlineData(@"Documents\Query\query-service-error-response-503.json", QueryStatus.Errors)]
        [InlineData(@"Documents\Query\query-timeout-response-200.json", QueryStatus.Timeout)]
        public async Task InitializeAsync_NoResults_HasErrors(string filename, QueryStatus expectedStatus)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);

            // Act

            await streamingResult.InitializeAsync();

            // Assert

            Assert.False(streamingResult.Success);
            Assert.Equal(expectedStatus, streamingResult.MetaData.Status);
            Assert.NotEmpty(streamingResult.Errors);
        }

        [Fact]
        public async Task InitializeAsync_ErrorAfterEmptyResults_HasErrors()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-errors-after-empty-results.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);
            streamingResult.Success = true; // In this scenario we're getting a 200, so Success will be set to true
            await streamingResult.InitializeAsync();

            // Act

            await streamingResult.InitializeAsync();

            // Assert

            Assert.False(streamingResult.Success);
            Assert.Equal(QueryStatus.Fatal, streamingResult.MetaData.Status);
            Assert.NotEmpty(streamingResult.Errors);
        }

        #endregion

        #region GetAsyncEnumerator

        [Fact]
        public async Task GetAsyncEnumerator_HasInitialized_GetsResults()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);
            streamingResult.Success = true;
            await streamingResult.InitializeAsync();

            // Act

            var result = await streamingResult.ToListAsync();

            // Assert

            Assert.True(streamingResult.Success);
            Assert.Equal(QueryStatus.Success, streamingResult.MetaData.Status);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GetAsyncEnumerator_NoResults_Empty()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success-empty.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);
            await streamingResult.InitializeAsync();

            // Act

            var result = await streamingResult.ToListAsync();

            // Assert

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAsyncEnumerator_HasNotInitialized_InvalidOperationException()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);

            // Act/Assert

            await Assert.ThrowsAsync<InvalidOperationException>(() => streamingResult.ToListAsync().AsTask());
        }

        [Fact]
        public async Task GetAsyncEnumerator_CalledTwice_StreamAlreadyReadException()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);
            await streamingResult.InitializeAsync();

            // Act/Assert

            await streamingResult.ToListAsync();
            await Assert.ThrowsAsync<StreamAlreadyReadException>(() => streamingResult.ToListAsync().AsTask());
        }

        [Theory]
        [InlineData(@"Documents\Query\query-200-errors-after-some-results.json")]
        public async Task GetAsyncEnumerator_ErrorAfterResults_Throws(string filename)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);
            streamingResult.Success = true; // In this scenario we're getting a 200, so Success will be set to true
            await streamingResult.InitializeAsync();

            // Act/Assert

            await Assert.ThrowsAsync<CouchbaseException>(() => streamingResult.ToListAsync().AsTask());
        }

        [Theory]
        [InlineData(@"Documents\Query\query-200-errors-after-empty-results.json")]
        public async Task GetAsyncEnumerator_ErrorAfterResults_13014_Throws_AuthenticationException(string filename)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);
            streamingResult.Success = true; // In this scenario we're getting a 200, so Success will be set to true
            await streamingResult.InitializeAsync();

            // Act/Assert

            await Assert.ThrowsAsync<AuthenticationFailureException>(() => streamingResult.ToListAsync().AsTask());
        }

        [Fact]
        public async Task GetAsyncEnumerator_AfterEnumeration_PreResultFieldsStillPresent()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);
            streamingResult.Success = true; // In this scenario we're getting a 200, so Success will be set to true
            await streamingResult.InitializeAsync();

            // Act

            await streamingResult.ToListAsync();

            // Assert

            Assert.Equal("b7a6b094-4699-4edb-b576-9092ab1404cb", streamingResult.MetaData.RequestId);
            Assert.Equal("1e6df61d-29ef-4821-9e49-02b3edd06ce5", streamingResult.MetaData.ClientContextId);
            Assert.NotNull(streamingResult.MetaData.Signature);
        }

        #endregion

        #region ReadToRowsAsync

        [Fact]
        public async Task InitializeAsync_Success_PreResultFieldsPresent()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);

            // Act

            await streamingResult.InitializeAsync();

            // Assert

            Assert.Equal("b7a6b094-4699-4edb-b576-9092ab1404cb", streamingResult.MetaData.RequestId);
            Assert.Equal("1e6df61d-29ef-4821-9e49-02b3edd06ce5", streamingResult.MetaData.ClientContextId);
            Assert.NotNull(streamingResult.MetaData.Signature);
        }

        [Fact]
        public async Task InitializeAsync_Error_AllResultFieldsPresent()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);

            // Act

            await streamingResult.InitializeAsync();

            // Assert

            Assert.Equal("922edd9a-23d7-4053-8d60-91f7cbc22c83", streamingResult.MetaData.RequestId);
            Assert.NotEmpty(streamingResult.Errors);
            Assert.Equal(QueryStatus.Fatal, streamingResult.MetaData.Status);
            Assert.NotNull(streamingResult.MetaData.Metrics);
            Assert.Equal("134.7944us", streamingResult.MetaData.Metrics.ElapsedTime);
        }

        [Fact]
        public async Task InitializeAsync_Error_Includes_Reason()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\failure_cas_mismatch_reason.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);

            // Act

            await streamingResult.InitializeAsync();

            // Assert

            Assert.NotEmpty(streamingResult.Errors);
            Assert.NotNull(streamingResult.Errors.First().Reason);
            Assert.Equal("CAS mismatch", streamingResult.Errors.First().Reason.Message);
            Assert.Equal("couchbase:2065", streamingResult.Errors.First().Reason.Caller);
            Assert.Equal(12033, streamingResult.Errors.First().Reason.Code);
            Assert.Equal("datastore.couchbase.CAS_mismatch", streamingResult.Errors.First().Reason.Key);
            Assert.False(streamingResult.Errors.First().Retry);
            Assert.Equal(QueryStatus.Fatal, streamingResult.MetaData.Status);
            Assert.NotNull(streamingResult.MetaData.Metrics);
        }

        [Fact]
        public async Task InitializeAsync_Error_CreateIndexInternalServerError()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-create-index-500.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer(), ErrorContextFactory);

            // Act

            await streamingResult.InitializeAsync();

            // Assert

            Assert.Equal("d5017926-d7a4-41bf-bb13-b788885f9c08", streamingResult.MetaData.RequestId);
            Assert.NotEmpty(streamingResult.Errors);
            Assert.Equal(QueryStatus.Errors, streamingResult.MetaData.Status);
            Assert.NotNull(streamingResult.MetaData.Metrics);
        }

        #endregion

        #region Natural language

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InitializeAsync_GeneratedStatementExecuted_FieldsBeforeResultsPresent(bool useSystemTextJson)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-natural-language-executed.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, CreateDeserializer(useSystemTextJson), ErrorContextFactory);
            streamingResult.Success = true; // In this scenario we're getting a 200, so Success will be set to true

            // Act

            await streamingResult.InitializeAsync();

            // Assert

            var naturalLanguage = streamingResult.NaturalLanguage;
            Assert.Equal(
                "SELECT COUNT(*) AS `count` FROM `travel-sample`.`inventory`.`hotel` AS `h` WHERE `h`.`country` = \"United States\"",
                naturalLanguage.GeneratedStatement);
            Assert.Equal(812, naturalLanguage.RequestTokens.PromptTokens);
            Assert.Equal(31, naturalLanguage.RequestTokens.CompletionTokens);
            Assert.Equal(843, naturalLanguage.RequestTokens.TotalTokens);
            Assert.Equal(1630, naturalLanguage.ChatTokens.PromptTokens);
            Assert.Equal(60, naturalLanguage.ChatTokens.CompletionTokens);
            Assert.Equal(1690, naturalLanguage.ChatTokens.TotalTokens);
            Assert.Null(naturalLanguage.ChatId);
            Assert.Null(naturalLanguage.NaturalLanguageProcessingTime);
            Assert.Null(naturalLanguage.GeneratedStatementExecuted);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetAsyncEnumerator_GeneratedStatementExecuted_MetricsPresentAfterRows(bool useSystemTextJson)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-natural-language-executed.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, CreateDeserializer(useSystemTextJson), ErrorContextFactory);
            streamingResult.Success = true; // In this scenario we're getting a 200, so Success will be set to true
            await streamingResult.InitializeAsync();

            // Act

            var result = await streamingResult.ToListAsync();

            // Assert

            Assert.Single(result);
            Assert.True(streamingResult.Success);
            Assert.Equal("1.990032281s", streamingResult.MetaData.Metrics.ElapsedTime);
            Assert.Equal(TimeSpan.FromTicks(19_836_398), streamingResult.NaturalLanguage.NaturalLanguageProcessingTime);
            Assert.True(streamingResult.NaturalLanguage.GeneratedStatementExecuted);
            Assert.Equal(843, streamingResult.NaturalLanguage.RequestTokens.TotalTokens);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InitializeAsync_NothingExecuted_AllFieldsPresent(bool useSystemTextJson)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-natural-language-not-executed.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, CreateDeserializer(useSystemTextJson), ErrorContextFactory);
            streamingResult.Success = true; // In this scenario we're getting a 200, so Success will be set to true

            // Act

            await streamingResult.InitializeAsync();

            // Assert

            var naturalLanguage = streamingResult.NaturalLanguage;
            Assert.True(streamingResult.Success);
            Assert.Equal(
                "SELECT `h`.`country`, COUNT(*) AS `count` FROM `travel-sample`.`inventory`.`hotel` AS `h` GROUP BY `h`.`country`",
                naturalLanguage.GeneratedStatement);
            Assert.Equal(700, naturalLanguage.RequestTokens.PromptTokens);
            Assert.Null(naturalLanguage.RequestTokens.CompletionTokens);
            Assert.Null(naturalLanguage.RequestTokens.TotalTokens);
            Assert.Null(naturalLanguage.ChatTokens);
            Assert.Equal("23a2c880-4566-4f78-9e81-86ea755b2a7f", naturalLanguage.ChatId);
            Assert.Equal(TimeSpan.FromTicks(28_253_839), naturalLanguage.NaturalLanguageProcessingTime);
            Assert.False(naturalLanguage.GeneratedStatementExecuted);
            Assert.Empty(await streamingResult.ToListAsync());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InitializeAsync_BeginChat_HasChatIdOnly(bool useSystemTextJson)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-begin-chat.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, CreateDeserializer(useSystemTextJson), ErrorContextFactory);
            streamingResult.Success = true; // In this scenario we're getting a 200, so Success will be set to true

            // Act

            await streamingResult.InitializeAsync();

            // Assert

            var naturalLanguage = streamingResult.NaturalLanguage;
            Assert.True(streamingResult.Success);
            Assert.Equal("23a2c880-4566-4f78-9e81-86ea755b2a7f", naturalLanguage.ChatId);
            Assert.Null(naturalLanguage.GeneratedStatement);
            Assert.Null(naturalLanguage.RequestTokens);
            Assert.Null(naturalLanguage.ChatTokens);
            Assert.Null(naturalLanguage.NaturalLanguageProcessingTime);
            Assert.Null(naturalLanguage.GeneratedStatementExecuted);
            Assert.Equal("85.322µs", streamingResult.MetaData.Metrics.ElapsedTime);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InitializeAsync_ChatNotFound_HasError(bool useSystemTextJson)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-chat-not-found-error.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, CreateDeserializer(useSystemTextJson), ErrorContextFactory);

            // Act

            await streamingResult.InitializeAsync();

            // Assert

            Assert.False(streamingResult.Success);
            Assert.Equal(QueryStatus.Fatal, streamingResult.MetaData.Status);
            Assert.Equal(19236, Assert.Single(streamingResult.Errors).Code);
            Assert.Null(streamingResult.NaturalLanguage.ChatId);
            Assert.Null(streamingResult.NaturalLanguage.GeneratedStatement);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InitializeAsync_InvalidProcessingTime_LeavesTimeUnset(bool useSystemTextJson)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-natural-language-bad-duration.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, CreateDeserializer(useSystemTextJson), ErrorContextFactory);
            streamingResult.Success = true; // In this scenario we're getting a 200, so Success will be set to true

            // Act

            await streamingResult.InitializeAsync();

            // Assert

            Assert.True(streamingResult.Success);
            Assert.Equal("SELECT 1", streamingResult.NaturalLanguage.GeneratedStatement);
            Assert.Null(streamingResult.NaturalLanguage.NaturalLanguageProcessingTime);
            Assert.False(streamingResult.NaturalLanguage.GeneratedStatementExecuted);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetAsyncEnumerator_RegularQuery_NoNaturalLanguageFields(bool useSystemTextJson)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, CreateDeserializer(useSystemTextJson), ErrorContextFactory);
            streamingResult.Success = true; // In this scenario we're getting a 200, so Success will be set to true
            await streamingResult.InitializeAsync();

            // Act

            await streamingResult.ToListAsync();

            // Assert

            var naturalLanguage = streamingResult.NaturalLanguage;
            Assert.Null(naturalLanguage.GeneratedStatement);
            Assert.Null(naturalLanguage.RequestTokens);
            Assert.Null(naturalLanguage.ChatTokens);
            Assert.Null(naturalLanguage.ChatId);
            Assert.Null(naturalLanguage.NaturalLanguageProcessingTime);
            Assert.Null(naturalLanguage.GeneratedStatementExecuted);
        }

        #endregion

        #region Error reason

        [Theory]
        [InlineData(false, "\"plain reason\"", 0, "plain reason", null)]
        [InlineData(true, "\"plain reason\"", 0, "plain reason", null)]
        [InlineData(false, "{\"caller\":\"c\",\"code\":19240,\"key\":\"k\",\"message\":\"with message\"}", 19240, "with message", "c")]
        [InlineData(true, "{\"caller\":\"c\",\"code\":19240,\"key\":\"k\",\"message\":\"with message\"}", 19240, "with message", "c")]
        [InlineData(false, "{\"code\":19240,\"msg\":\"with msg\",\"extra\":{\"a\":[1]}}", 19240, "with msg", null)]
        [InlineData(true, "{\"code\":19240,\"msg\":\"with msg\",\"extra\":{\"a\":[1]}}", 19240, "with msg", null)]
        [InlineData(false, "{\"msg\":\"ignored\",\"message\":\"wins\"}", 0, "wins", null)]
        [InlineData(true, "{\"msg\":\"ignored\",\"message\":\"wins\"}", 0, "wins", null)]
        public async Task InitializeAsync_Reason_Shapes_Are_Read(bool useSystemTextJson, string reason,
            int expectedCode, string expectedMessage, string expectedCaller)
        {
            var json = $$"""{"requestID":"r-1","errors":[{"code":19243,"msg":"outer","reason":{{reason}},"retry":true}],"status":"fatal"}""";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, CreateDeserializer(useSystemTextJson), ErrorContextFactory);
            await streamingResult.InitializeAsync();

            Assert.Equal(QueryStatus.Fatal, streamingResult.MetaData.Status);
            var error = Assert.Single(streamingResult.Errors);
            Assert.Equal(19243, error.Code);
            Assert.Equal("outer", error.Message);
            Assert.True(error.Retry);
            Assert.Equal(expectedCode, error.Reason.Code);
            Assert.Equal(expectedMessage, error.Reason.Message);
            Assert.Equal(expectedCaller, error.Reason.Caller);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InitializeAsync_Reason_Of_Other_Type_Is_Null(bool useSystemTextJson)
        {
            var json = """{"requestID":"r-1","errors":[{"code":5000,"msg":"outer","reason":[1,{"a":2}],"retry":true}],"status":"fatal"}""";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, CreateDeserializer(useSystemTextJson), ErrorContextFactory);
            await streamingResult.InitializeAsync();

            var error = Assert.Single(streamingResult.Errors);
            Assert.Null(error.Reason);
            Assert.True(error.Retry);
        }

        [Fact]
        public void Reason_Serializes_As_Object()
        {
            var context = new QueryErrorContext
            {
                Errors = new() { new Error { Code = 19243, Reason = new Reason { Code = 19240, Message = "busy" } } }
            };
            const string expected = "\"reason\":{\"caller\":null,\"code\":19240,\"key\":null,\"message\":\"busy\"}";

            Assert.Contains(expected, JsonSerializer.Serialize(context, QuerySerializerContext.Default.QueryErrorContext));
            Assert.Contains(expected, Newtonsoft.Json.JsonConvert.SerializeObject(context));
        }

        #endregion

        #region Helpers

        // Options without camel case show that the internal types are read through the SDK's own context.
        private static IStreamingTypeDeserializer CreateDeserializer(bool useSystemTextJson) =>
            useSystemTextJson
                ? SystemTextJsonSerializer.Create(new JsonSerializerOptions())
                : new DefaultSerializer();

        private QueryErrorContext ErrorContextFactory<T>(QueryResultBase<T> failedQueryResult,
            HttpStatusCode statusCode) =>
            new QueryErrorContext
            {
                ClientContextId = Guid.Empty.ToString(),
                Parameters = "{}",
                Statement = "",
                Message = "Error Message",
                Errors = failedQueryResult.Errors,
                HttpStatus = statusCode,
                QueryStatus = failedQueryResult.MetaData?.Status ?? QueryStatus.Fatal
            };

        #endregion
    }
}
