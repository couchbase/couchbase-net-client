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

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());
            await blockResult.InitializeAsync().ConfigureAwait(false);

            // Act

            var result = await blockResult.ToListAsync().ConfigureAwait(false);

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
            await blockResult.InitializeAsync().ConfigureAwait(false);

            // Act

            var result = await blockResult.ToListAsync().ConfigureAwait(false);

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

            await Assert.ThrowsAsync<InvalidOperationException>(() => blockResult.ToListAsync().AsTask()).ConfigureAwait(false);
        }

        [Theory]
        [InlineData(@"Documents\Query\query-200-success.json")]
        [InlineData(@"Documents\Query\query-n1ql-error-response-400.json")]
        public async Task GetAsyncEnumerator_CalledTwice_StreamAlreadyReadException(string filename)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var blockResult = new BlockQueryResult<dynamic>(stream, new DefaultSerializer());
            await blockResult.InitializeAsync().ConfigureAwait(false);

            // Act/Assert

            await blockResult.ToListAsync().ConfigureAwait(false);
            await Assert.ThrowsAsync<StreamAlreadyReadException>(() => blockResult.ToListAsync().AsTask()).ConfigureAwait(false);
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
            await blockResult.InitializeAsync().ConfigureAwait(false);

            // Act

            await blockResult.ToListAsync().ConfigureAwait(false);
            var result = blockResult.MetaData.Status;

            // Assert

            Assert.False(blockResult.Success);
            Assert.Equal(expectedStatus, result);
            Assert.NotEmpty(blockResult.Errors);
        }

        #endregion
    }
}
