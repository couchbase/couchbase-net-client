using System.Net;
using Couchbase.Core.Retry;
using Couchbase.Views;
using Xunit;

namespace Couchbase.UnitTests.Services.Views
{
    public class ViewResultTests
    {
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
    }
}
