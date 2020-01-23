using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Xunit;

namespace Couchbase.UnitTests.Services.Views
{
    public class ViewResultTests
    {
        private const string ViewResultResourceName = @"Documents\Views\200-success.json";

        #region Properties

        [Fact]
        public async Task Test_StatusCode()
        {
            const HttpStatusCode statusCode = HttpStatusCode.Accepted;
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var serializer = new DefaultSerializer();
            var response = new ViewResult(statusCode, string.Empty, stream, serializer);
            await response.InitializeAsync();

            Assert.Equal(statusCode, response.StatusCode);
        }

        [Fact]
        public async Task Test_Message()
        {
            const string message = "message";
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var serializer = new DefaultSerializer();
            var response = new ViewResult(HttpStatusCode.OK, message, stream, serializer);
            await response.InitializeAsync();

            Assert.Equal(message, response.Message);
        }

        [Fact]
        public async Task Test_TotalRows()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var serializer = new DefaultSerializer();
            var response = new ViewResult(HttpStatusCode.OK, string.Empty, stream, serializer);
            await response.InitializeAsync();

            await foreach (var row in response)
            {
                // noop
            }

            Assert.Equal(4u, response.MetaData.TotalRows);
        }

        /*[Theory]
        [InlineData(HttpStatusCode.OK, "some error", RetryReason.NoRetry)]
        [InlineData(HttpStatusCode.NotFound, "\"reason\": \"missing\"", RetryReason.ViewsTemporaryFailure)]
        [InlineData(HttpStatusCode.NotFound, "{not_found, missing}", false)]
        [InlineData(HttpStatusCode.NotFound, "{not_found, deleted}", false)]
        [InlineData(HttpStatusCode.InternalServerError, "error - some error", true)]
        [InlineData(HttpStatusCode.InternalServerError, "{not_found, missing_named_view}", true)]
        [InlineData(HttpStatusCode.InternalServerError, "error - {not_found, missing_named_view}", false)]
        public void Failed_ViewResult_Should_Retry_Based_On_StatusCode_And_Message(HttpStatusCode statusCode, string message, RetryReason expected)
        {
            var viewResult = new ViewResult(statusCode, message);
            var serviceResult = (IServiceResult) viewResult;
            Assert.NotEqual(RetryReason.NoRetry, serviceResult.RetryReason);
        }*/

        #endregion

        #region GetAsyncEnumerator

        [Fact]
        public async Task GetAsyncEnumerator_HasInitialized_GetsResults()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var viewResult = new ViewResult(HttpStatusCode.OK, "OK", stream, new DefaultSerializer());
            await viewResult.InitializeAsync();

            // Act

            var result = await viewResult.ToListAsync();

            // Assert

            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GetAsyncEnumerator_NoStream_Empty()
        {
            // Arrange

            using var viewResult = new ViewResult(HttpStatusCode.OK, "OK", new DefaultSerializer());

            // Act

            await viewResult.InitializeAsync();
            var result = await viewResult.ToListAsync();

            // Assert

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAsyncEnumerator_HasNotInitialized_InvalidOperationException()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var viewResult = new ViewResult(HttpStatusCode.OK, "OK", stream, new DefaultSerializer());

            // Act/Assert

            await Assert.ThrowsAsync<InvalidOperationException>(() => viewResult.ToListAsync().AsTask());
        }

        [Theory]
        [InlineData(@"Documents\Views\200-success.json")]
        [InlineData(@"Documents\Views\404-view-notfound.json")]
        public async Task GetAsyncEnumerator_CalledTwice_StreamAlreadyReadException(string filename)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var viewResult = new ViewResult(HttpStatusCode.OK, "OK", stream, new DefaultSerializer());
            await viewResult.InitializeAsync();

            // Act/Assert

            await viewResult.ToListAsync();
            await Assert.ThrowsAsync<StreamAlreadyReadException>(() => viewResult.ToListAsync().AsTask());
        }

        [Fact]
        public async Task GetAsyncEnumerator_AfterEnumeration_PreResultFieldsStillPresent()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var viewResult = new ViewResult(HttpStatusCode.OK, "OK", stream, new DefaultSerializer());
            await viewResult.InitializeAsync();

            // Act

            await viewResult.ToListAsync();

            // Assert

            Assert.Equal(4u, viewResult.MetaData.TotalRows);
        }

        #endregion

        #region InitializeAsync

        [Fact]
        public async Task InitializeAsync_Success_PreResultFieldsPresent()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var viewResult = new ViewResult(HttpStatusCode.OK, "OK", stream, new DefaultSerializer());

            // Act

            await viewResult.InitializeAsync();

            // Assert

            Assert.Equal(4u, viewResult.MetaData.TotalRows);
        }

        [Fact]
        public async Task InitializeAsync_CalledTwice_InvalidOperationException()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var viewResult = new ViewResult(HttpStatusCode.OK, "OK", stream, new DefaultSerializer());

            // Act/Assert

            await viewResult.InitializeAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await viewResult.InitializeAsync());
        }

        #endregion
    }
}
