using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Xunit;

namespace Couchbase.UnitTests.Services.Query
{
    public class StreamingQueryResultTests
    {
        #region GetAsyncEnumerator

        [Fact]
        public async Task GetAsyncEnumerator_HasReadToRows_GetsResults()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());
            await streamingResult.ReadToRowsAsync(default);

            // Act

            var result = await streamingResult.ToListAsync();

            // Assert

            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GetAsyncEnumerator_NoResults_Empty()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());
            await streamingResult.ReadToRowsAsync(default);

            // Act

            var result = await streamingResult.ToListAsync();

            // Assert

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAsyncEnumerator_HasNotReadToRows_InvalidOperationException()
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
            await streamingResult.ReadToRowsAsync(default);

            // Act/Assert

            await streamingResult.ToListAsync();
            await Assert.ThrowsAsync<StreamAlreadyReadException>(() => streamingResult.ToListAsync().AsTask());
        }

        [Fact]
        public async Task GetAsyncEnumerator_AfterEnumeration_PreResultFieldsStillPresent()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());
            await streamingResult.ReadToRowsAsync(default);

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
        public async Task ReadToRowsAsync_Success_PreResultFieldsPresent()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());

            // Act

            await streamingResult.ReadToRowsAsync(default);

            // Assert

            Assert.Equal("b7a6b094-4699-4edb-b576-9092ab1404cb", streamingResult.MetaData.RequestId);
            Assert.Equal("1e6df61d-29ef-4821-9e49-02b3edd06ce5", streamingResult.MetaData.ClientContextId);
            Assert.NotNull(streamingResult.MetaData.Signature);
        }

        [Fact]
        public async Task ReadToRowsAsync_Error_AllResultFieldsPresent()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var streamingResult = new StreamingQueryResult<dynamic>(stream, new DefaultSerializer());

            // Act

            await streamingResult.ReadToRowsAsync(default);

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
