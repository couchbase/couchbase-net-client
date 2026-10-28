using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Serializers.SystemTextJson;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Xunit;

namespace Couchbase.UnitTests.Query
{
    public class BlockQueryResultTests
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

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());
            await blockResult.InitializeAsync();

            // Act

            var actual = blockResult.ShouldRetry(enableEnhancedPreparedStatements);

            // Assert

            Assert.Equal(shouldRetry, actual);
        }

        #endregion

        #region GetAsyncEnumerator

        [Fact]
        public async Task GetAsyncEnumerator_HasInitialized_GetsResults()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());
            await blockResult.InitializeAsync();

            // Act

            var result = await blockResult.ToListAsync();

            // Assert
            Assert.True(blockResult.Success);
            Assert.Equal(QueryStatus.Success, blockResult.MetaData.Status);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GetAsyncEnumerator_NoResults_Empty()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());
            await blockResult.InitializeAsync();

            // Act

            var result = await blockResult.ToListAsync();

            // Assert

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAsyncEnumerator_HasNotInitialized_InvalidOperationException()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());

            // Act/Assert

            await Assert.ThrowsAsync<InvalidOperationException>(() => blockResult.ToListAsync().AsTask());
        }

        [Theory]
        [InlineData(@"Documents\Query\query-200-success.json")]
        [InlineData(@"Documents\Query\query-n1ql-error-response-400.json")]
        public async Task GetAsyncEnumerator_CalledTwice_StreamAlreadyReadException(string filename)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());
            await blockResult.InitializeAsync();

            // Act/Assert

            await blockResult.ToListAsync();
            await Assert.ThrowsAsync<StreamAlreadyReadException>(() => blockResult.ToListAsync().AsTask());
        }

        [Theory]
        [InlineData(@"Documents\Query\query-n1ql-error-response-400.json", QueryStatus.Fatal)]
        [InlineData(@"Documents\Query\query-service-error-response-503.json", QueryStatus.Errors)]
        [InlineData(@"Documents\Query\query-timeout-response-200.json", QueryStatus.Timeout)]
        public async Task GetAsyncEnumerator_AfterEnumeration_HasErrors(string filename, QueryStatus expectedStatus)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());
            await blockResult.InitializeAsync();

            // Act

            await blockResult.ToListAsync();
            var result = blockResult.MetaData.Status;

            // Assert

            Assert.False(blockResult.Success);
            Assert.Equal(expectedStatus, result);
            Assert.NotEmpty(blockResult.Errors);
        }

        [Fact]
        public async Task InitializeAsync_Error_Includes_Reason()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\failure_cas_mismatch_reason.json");

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());
            await blockResult.InitializeAsync();

            // Act

            await blockResult.ToListAsync();
            var result = blockResult.MetaData.Status;

            Assert.NotEmpty(blockResult.Errors);
            Assert.NotNull(blockResult.Errors.First().Reason);
            Assert.Equal("CAS mismatch", blockResult.Errors.First().Reason.Message);
            Assert.Equal("couchbase:2065", blockResult.Errors.First().Reason.Caller);
            Assert.Equal(12033, blockResult.Errors.First().Reason.Code);
            Assert.Equal("datastore.couchbase.CAS_mismatch", blockResult.Errors.First().Reason.Key);
            Assert.False(blockResult.Errors.First().Retry);
            Assert.Equal(QueryStatus.Fatal, blockResult.MetaData.Status);
            Assert.NotNull(blockResult.MetaData.Metrics);
        }

        #endregion

        #region Natural language

        [Fact]
        public async Task InitializeAsync_GeneratedStatementExecuted_AllFieldsPresent()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-natural-language-executed.json");

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());

            // Act

            await blockResult.InitializeAsync();

            // Assert

            var naturalLanguage = blockResult.NaturalLanguage;
            Assert.True(blockResult.Success);
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
            Assert.Equal(TimeSpan.FromTicks(19_836_398), naturalLanguage.NaturalLanguageProcessingTime);
            Assert.True(naturalLanguage.GeneratedStatementExecuted);
            Assert.Single(await blockResult.ToListAsync());
        }

        [Fact]
        public async Task InitializeAsync_NothingExecuted_AllFieldsPresent()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-natural-language-not-executed.json");

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());

            // Act

            await blockResult.InitializeAsync();

            // Assert

            var naturalLanguage = blockResult.NaturalLanguage;
            Assert.True(blockResult.Success);
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
            Assert.Empty(await blockResult.ToListAsync());
        }

        [Fact]
        public async Task InitializeAsync_BeginChat_HasChatIdOnly()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-begin-chat.json");

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());

            // Act

            await blockResult.InitializeAsync();

            // Assert

            var naturalLanguage = blockResult.NaturalLanguage;
            Assert.True(blockResult.Success);
            Assert.Equal("23a2c880-4566-4f78-9e81-86ea755b2a7f", naturalLanguage.ChatId);
            Assert.Null(naturalLanguage.GeneratedStatement);
            Assert.Null(naturalLanguage.RequestTokens);
            Assert.Null(naturalLanguage.ChatTokens);
            Assert.Null(naturalLanguage.NaturalLanguageProcessingTime);
            Assert.Null(naturalLanguage.GeneratedStatementExecuted);
            Assert.Equal("85.322µs", blockResult.MetaData.Metrics.ElapsedTime);
        }

        [Fact]
        public async Task InitializeAsync_ChatNotFound_HasError()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-chat-not-found-error.json");

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());

            // Act

            await blockResult.InitializeAsync();

            // Assert

            Assert.False(blockResult.Success);
            Assert.Equal(QueryStatus.Fatal, blockResult.MetaData.Status);
            Assert.Equal(19236, Assert.Single(blockResult.Errors).Code);
            Assert.Null(blockResult.NaturalLanguage.ChatId);
            Assert.Null(blockResult.NaturalLanguage.GeneratedStatement);
        }

        [Fact]
        public async Task InitializeAsync_InvalidProcessingTime_LeavesTimeUnset()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-natural-language-bad-duration.json");

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());

            // Act

            await blockResult.InitializeAsync();

            // Assert

            Assert.True(blockResult.Success);
            Assert.Equal("SELECT 1", blockResult.NaturalLanguage.GeneratedStatement);
            Assert.Null(blockResult.NaturalLanguage.NaturalLanguageProcessingTime);
            Assert.False(blockResult.NaturalLanguage.GeneratedStatementExecuted);
        }

        [Fact]
        public async Task InitializeAsync_RegularQuery_NoNaturalLanguageFields()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());

            // Act

            await blockResult.InitializeAsync();

            // Assert

            var naturalLanguage = blockResult.NaturalLanguage;
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
            var json = $$"""{"requestID":"5e0f3c2a-7d41-4b6e-a8c9-1f2d3e4b5a60","errors":[{"code":19243,"msg":"outer","reason":{{reason}},"retry":true}],"status":"fatal"}""";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            ITypeSerializer serializer = useSystemTextJson ? SystemTextJsonSerializer.Create() : new DefaultSerializer();

            using var blockResult = new BlockQueryResult<dynamic>(stream, serializer);
            await blockResult.InitializeAsync();

            Assert.Equal(QueryStatus.Fatal, blockResult.MetaData.Status);
            var error = Assert.Single(blockResult.Errors);
            Assert.Equal(19243, error.Code);
            Assert.Equal("outer", error.Message);
            Assert.True(error.Retry);
            Assert.Equal(expectedCode, error.Reason.Code);
            Assert.Equal(expectedMessage, error.Reason.Message);
            Assert.Equal(expectedCaller, error.Reason.Caller);
        }

        #endregion
    }
}
