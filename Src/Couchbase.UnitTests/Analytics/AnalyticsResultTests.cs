using System.Collections.Generic;
using Couchbase.Analytics;
using Couchbase.N1QL;
using NUnit.Framework;

namespace Couchbase.UnitTests.Analytics
{
    [TestFixture]
    public class AnalyticsResultTests
    {
        [TestCase(21002, QueryStatus.Fatal)]
        [TestCase(23000, QueryStatus.Fatal)]
        [TestCase(23003, QueryStatus.Fatal)]
        [TestCase(23007, QueryStatus.Fatal)]
        [TestCase(21002, QueryStatus.Timeout)]
        [TestCase(23000, QueryStatus.Timeout)]
        [TestCase(23003, QueryStatus.Timeout)]
        [TestCase(23007, QueryStatus.Timeout)]
        [TestCase(21002, QueryStatus.Errors)]
        [TestCase(23000, QueryStatus.Errors)]
        [TestCase(23003, QueryStatus.Errors)]
        [TestCase(23007, QueryStatus.Errors)]
        public void Should_return_true_for_retryable_error_code(int errorCode, QueryStatus status)
        {
            var result = new AnalyticsResult<dynamic>
            {
                Status = QueryStatus.Fatal,
                Errors = new List<Error> {new Error {Code = errorCode}}
            };

            Assert.IsTrue(result.ShouldRetry());
        }
    }
}
