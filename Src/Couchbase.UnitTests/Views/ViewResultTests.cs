using System.Net;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.UnitTests.Views
{
    [TestFixture]
    public class ViewResultTests
    {
        [TestCase(HttpStatusCode.NotFound, "some error", true)]
        [TestCase(HttpStatusCode.NotFound, "{not_found, missing}", false)]
        [TestCase(HttpStatusCode.NotFound, "{not_found, deleted}", false)]
        [TestCase(HttpStatusCode.InternalServerError, "error - some error", true)]
        [TestCase(HttpStatusCode.InternalServerError, "{not_found, missing_named_view}", true)]
        [TestCase(HttpStatusCode.InternalServerError, "error - {not_found, missing_named_view}", false)]
        public void Failed_ViewResult_Should_Retry_Based_On_StatusCode_And_Message(HttpStatusCode statusCode, string message, bool expected)
        {
            var viewResult = new ViewResult<dynamic>
            {
                StatusCode = statusCode,
                Error = message
            };

            Assert.AreEqual(expected, viewResult.ShouldRetry());
            Assert.AreEqual(expected, !viewResult.CannotRetry());
        }
    }
}
