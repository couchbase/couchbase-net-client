using System.Net;
using Couchbase.Views;
using Xunit;

namespace Couchbase.UnitTests.Services.Views
{
    public class ViewResultTests
    {
        [Theory]
        [InlineData(HttpStatusCode.OK, "some error", false)]
        [InlineData(HttpStatusCode.NotFound, "some error", true)]
        [InlineData(HttpStatusCode.NotFound, "{not_found, missing}", false)]
        [InlineData(HttpStatusCode.NotFound, "{not_found, deleted}", false)]
        [InlineData(HttpStatusCode.InternalServerError, "error - some error", true)]
        [InlineData(HttpStatusCode.InternalServerError, "{not_found, missing_named_view}", true)]
        [InlineData(HttpStatusCode.InternalServerError, "error - {not_found, missing_named_view}", false)]
        public void Failed_ViewResult_Should_Retry_Based_On_StatusCode_And_Message(HttpStatusCode statusCode, string message, bool expected)
        {
            var viewResult = new ViewResult(statusCode, message);
            Assert.Equal(expected, viewResult.ShouldRetry());
        }
    }
}
