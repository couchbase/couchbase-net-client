using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Xunit;

namespace Couchbase.UnitTests.Analytics
{
    public class BlockAnalyticsResultTests
    {
        #region GetAsyncEnumerator

        [Fact]
        public async Task GetAsyncEnumerator_HasInitialized_GetsResults()
        {
            // Arrange
#if NET8_0_OR_GREATER
            await using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\good-request.json");
#else
            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\good-request.json");
#endif

            using var queryResult = new BlockAnalyticsResult<dynamic>(stream, new DefaultSerializer());
            await queryResult.InitializeAsync();

            // Act

            var result = await queryResult.ToListAsync();

            // Assert

            Assert.Equal(AnalyticsStatus.Success, queryResult.MetaData.Status);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GetAsyncEnumerator_NoResults_Empty()
        {
            // Arrange
#if NET8_0_OR_GREATER
           await  using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");
#else
            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");
#endif

            using var queryResult = new BlockAnalyticsResult<dynamic>(stream, new DefaultSerializer());
            await queryResult.InitializeAsync();

            // Act

            var result = await queryResult.ToListAsync();

            // Assert

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAsyncEnumerator_HasNotInitialized_InvalidOperationException()
        {
            // Arrange

#if NET8_0_OR_GREATER
            await using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\good-request.json");
#else
            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\good-request.json");
#endif

            using var queryResult = new BlockAnalyticsResult<dynamic>(stream, new DefaultSerializer());

            // Act/Assert

            await Assert.ThrowsAsync<InvalidOperationException>(() => queryResult.ToListAsync().AsTask());
        }

        [Theory]
        [InlineData(@"Documents\Analytics\good-request.json")]
        [InlineData(@"Documents\Analytics\syntax-24000.json")]
        public async Task GetAsyncEnumerator_CalledTwice_StreamAlreadyReadException(string filename)
        {
            // Arrange
#if NET8_0_OR_GREATER
            await using var stream = ResourceHelper.ReadResourceAsStream(filename);
#else
            using var stream = ResourceHelper.ReadResourceAsStream(filename);
#endif
            using var queryResult = new BlockAnalyticsResult<dynamic>(stream, new DefaultSerializer());
            await queryResult.InitializeAsync();

            // Act/Assert

            await queryResult.ToListAsync();
            await Assert.ThrowsAsync<StreamAlreadyReadException>(() => queryResult.ToListAsync().AsTask());
        }

        [Theory]
        [InlineData(@"Documents\Analytics\syntax-24000.json", AnalyticsStatus.Fatal)]
        [InlineData(@"Documents\Analytics\timeout.json", AnalyticsStatus.Timeout)]
        public async Task GetAsyncEnumerator_AfterEnumeration_HasErrors(string filename, AnalyticsStatus expectedStatus)
        {
            // Arrange
#if NET8_0_OR_GREATER
            await using var stream = ResourceHelper.ReadResourceAsStream(filename);
#else
            using var stream = ResourceHelper.ReadResourceAsStream(filename);
#endif

            using var queryResult = new BlockAnalyticsResult<dynamic>(stream, new DefaultSerializer());
            await queryResult.InitializeAsync();

            // Act

            await queryResult.ToListAsync();
            var result = queryResult.MetaData.Status;

            // Assert

            Assert.Equal(expectedStatus, result);
            Assert.NotEmpty(queryResult.Errors);
        }

        [Fact]
        public async Task GetAsyncEnumerator_AfterEnumeration_PreResultFieldsStillPresent()
        {
            // Arrange

#if NET8_0_OR_GREATER
            await using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\good-request.json");
#else
            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\good-request.json");
#endif
            using var queryResult = new BlockAnalyticsResult<dynamic>(stream, new DefaultSerializer());
            await queryResult.InitializeAsync();

            // Act

            await queryResult.ToListAsync();

            // Assert

            Assert.Equal("30f6bcdf-2288-4fe1-bea1-361bb96984a4", queryResult.MetaData.RequestId);
            Assert.NotNull(queryResult.MetaData.Signature);
        }

        #endregion

        #region ReadToRowsAsync

        [Fact]
        public async Task InitializeAsync_Success_PreResultFieldsPresent()
        {
            // Arrange

#if NET8_0_OR_GREATER
            await using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\good-request.json");
#else
            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\good-request.json");
#endif

            using var queryResult = new BlockAnalyticsResult<dynamic>(stream, new DefaultSerializer());

            // Act

            await queryResult.InitializeAsync();

            // Assert

            Assert.Equal("30f6bcdf-2288-4fe1-bea1-361bb96984a4", queryResult.MetaData.RequestId);
            Assert.NotNull(queryResult.MetaData.Signature);
        }

        [Fact]
        public async Task InitializeAsync_Error_AllResultFieldsPresent()
        {
            // Arrange

#if NET8_0_OR_GREATER
            await using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\syntax-24000.json");
#else
            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\syntax-24000.json");
#endif
            using var queryResult = new BlockAnalyticsResult<dynamic>(stream, new DefaultSerializer());

            // Act

            await queryResult.InitializeAsync();

            // Assert

            Assert.Equal("eb8a8d08-9e25-4473-81f8-6565c51a43d9", queryResult.MetaData.RequestId);
            Assert.NotEmpty(queryResult.Errors);
            Assert.Equal(AnalyticsStatus.Fatal, queryResult.MetaData.Status);
            Assert.NotNull(queryResult.MetaData.Metrics);
            Assert.Equal("161.673835ms", queryResult.MetaData.Metrics.ElaspedTime);
        }

        #endregion

        #region ShouldRetry

        [Theory]
        [InlineData(23000, AnalyticsStatus.Fatal)]
        [InlineData(23003, AnalyticsStatus.Fatal)]
        [InlineData(23007, AnalyticsStatus.Fatal)]
        [InlineData(23000, AnalyticsStatus.Timeout)]
        [InlineData(23003, AnalyticsStatus.Timeout)]
        [InlineData(23007, AnalyticsStatus.Timeout)]
        [InlineData(23000, AnalyticsStatus.Errors)]
        [InlineData(23003, AnalyticsStatus.Errors)]
        [InlineData(23007, AnalyticsStatus.Errors)]
        public void Should_return_true_for_retryable_error_code(int errorCode, AnalyticsStatus status)
        {
            using var result = new BlockAnalyticsResult<dynamic>(new MemoryStream(), new DefaultSerializer())
            {
                Errors = new List<Error> {new Error {Code = errorCode}},
                MetaData = new AnalyticsMetaData
                {
                    Status = status,
                }
            };

            Assert.True(result.ShouldRetry());
        }

        [Theory]
        [InlineData(21002, AnalyticsStatus.Fatal)]
        [InlineData(21002, AnalyticsStatus.Errors)]
        [InlineData(21002, AnalyticsStatus.Timeout)]
        public void Should_Throw_AmbiguousTimeoutException_For_Server_Timeout_Error_Code(int errorCode, AnalyticsStatus status)
        {
            using var result = new BlockAnalyticsResult<dynamic>(new MemoryStream(), new DefaultSerializer())
            {
                Errors = new List<Error> { new Error { Code = errorCode } },
                MetaData = new AnalyticsMetaData
                {
                    Status = status,
                }
            };

            Assert.Throws<AmbiguousTimeoutException>(() => result.ShouldRetry());
        }

        #endregion
    }
}
