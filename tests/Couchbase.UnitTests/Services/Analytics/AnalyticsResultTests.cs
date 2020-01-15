using System.Collections.Generic;
using Couchbase.Analytics;
using Couchbase.Query;
using Xunit;

namespace Couchbase.UnitTests.Services.Analytics
{
    public class AnalyticsResultTests
    {
        [Theory]
        [InlineData(21002, AnalyticsStatus.Fatal)]
        [InlineData(23000, AnalyticsStatus.Fatal)]
        [InlineData(23003, AnalyticsStatus.Fatal)]
        [InlineData(23007, AnalyticsStatus.Fatal)]
        [InlineData(21002, AnalyticsStatus.Timeout)]
        [InlineData(23000, AnalyticsStatus.Timeout)]
        [InlineData(23003, AnalyticsStatus.Timeout)]
        [InlineData(23007, AnalyticsStatus.Timeout)]
        [InlineData(21002, AnalyticsStatus.Errors)]
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
    }
}
