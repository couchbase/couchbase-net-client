using System.Collections.Generic;
using Couchbase.Analytics;
using Couchbase.Core.Exceptions;
using Couchbase.Query;
using Xunit;

namespace Couchbase.UnitTests.Services.Analytics
{
    public class AnalyticsResultTests
    {
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
            var result = new AnalyticsResult<dynamic>
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
            var result = new AnalyticsResult<dynamic>
            {
                Errors = new List<Error> { new Error { Code = errorCode } },
                MetaData = new AnalyticsMetaData
                {
                    Status = status,
                }
            };

            Assert.Throws<AmbiguousTimeoutException>(() =>result.ShouldRetry());
        }
    }
}
