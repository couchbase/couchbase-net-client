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

        #endregion
    }
}
