using System.Collections.Generic;
using Couchbase.Analytics;
using Couchbase.Query;
using Xunit;

namespace Couchbase.UnitTests.Services.Analytics
{
    public class AnalyticsResultTests
    {
        [Theory]
        [InlineData(21002, QueryStatus.Fatal)]
        [InlineData(23000, QueryStatus.Fatal)]
        [InlineData(23003, QueryStatus.Fatal)]
        [InlineData(23007, QueryStatus.Fatal)]
        [InlineData(21002, QueryStatus.Timeout)]
        [InlineData(23000, QueryStatus.Timeout)]
        [InlineData(23003, QueryStatus.Timeout)]
        [InlineData(23007, QueryStatus.Timeout)]
        [InlineData(21002, QueryStatus.Errors)]
        [InlineData(23000, QueryStatus.Errors)]
        [InlineData(23003, QueryStatus.Errors)]
        [InlineData(23007, QueryStatus.Errors)]
        public void Should_return_true_for_retryable_error_code(int errorCode, QueryStatus status)
        {
            var result = new AnalyticsResult<dynamic>
            {
                MetaData = new MetaData
                {
                    Status = status,
                    Errors = new List<Error> {new Error {Code = errorCode}}
                }
            };

            Assert.True(result.ShouldRetry());
        }
    }
}
