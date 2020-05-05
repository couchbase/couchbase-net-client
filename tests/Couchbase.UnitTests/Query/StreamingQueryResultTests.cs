using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Xunit;

namespace Couchbase.UnitTests.Query
{
    public class StreamingQueryResultTests
    {
        #region retry handling

        [Theory]
        [InlineData(@"Documents\Query\Retrys\4040.json", true, false)]
        [InlineData(@"Documents\Query\Retrys\4050.json", true, false)]
        [InlineData(@"Documents\Query\Retrys\4070.json", true, false)]
        [InlineData(@"Documents\Query\Retrys\5000.json", true, true)]
        [InlineData(@"Documents\Query\Retrys\4040.json", false, true)]
        [InlineData(@"Documents\Query\Retrys\4050.json", false, true)]
        [InlineData(@"Documents\Query\Retrys\4070.json", false, true)]
        [InlineData(@"Documents\Query\Retrys\5000.json", false, true)]
        public async Task ShouldRetry_Handles_Retry_Cases(string fileName, bool enableEnhancedPreparedStatements, bool shouldRetry)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(fileName);

            using var blockResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());
            await blockResult.InitializeAsync().ConfigureAwait(false);

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

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());
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

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());
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

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());

            // Act/Assert

            await Assert.ThrowsAsync<InvalidOperationException>(() => streamingResult.ToListAsync().AsTask());
        }

        [Theory]
        [InlineData(@"Documents\Query\query-200-success.json")]
        [InlineData(@"Documents\Query\query-n1ql-error-response-400.json")]
        public async Task GetAsyncEnumerator_CalledTwice_StreamAlreadyReadException(string filename)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());
            await streamingResult.InitializeAsync();

            // Act/Assert

            await streamingResult.ToListAsync();
            await Assert.ThrowsAsync<StreamAlreadyReadException>(() => streamingResult.ToListAsync().AsTask());
        }

        [Theory]
        [InlineData(@"Documents\Query\query-n1ql-error-response-400.json", QueryStatus.Fatal)]
        [InlineData(@"Documents\Query\query-service-error-response-503.json", QueryStatus.Errors)]
        [InlineData(@"Documents\Query\query-timeout-response-200.json", QueryStatus.Timeout)]
        public async Task GetAsyncEnumerator_AfterEnumeration_HasErrors(string filename, QueryStatus expectedStatus)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());
            await streamingResult.InitializeAsync();

            // Act

            await streamingResult.ToListAsync();
            var result = streamingResult.MetaData.Status;

            // Assert

            Assert.False(streamingResult.Success);
            Assert.Equal(expectedStatus, result);
            Assert.NotEmpty(streamingResult.Errors);
        }

        [Fact]
        public async Task GetAsyncEnumerator_AfterEnumeration_PreResultFieldsStillPresent()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());
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

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());

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

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());

            // Act

            await streamingResult.InitializeAsync();

            // Assert

            Assert.Equal("922edd9a-23d7-4053-8d60-91f7cbc22c83", streamingResult.MetaData.RequestId);
            Assert.NotEmpty(streamingResult.Errors);
            Assert.Equal(QueryStatus.Fatal, streamingResult.MetaData.Status);
            Assert.NotNull(streamingResult.MetaData.Metrics);
            Assert.Equal("134.7944us", streamingResult.MetaData.Metrics.ElaspedTime);
        }

        #endregion
    }
}
