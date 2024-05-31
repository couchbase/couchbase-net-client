using System;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Xunit;

namespace Couchbase.UnitTests.Core.Diagnostics.Metrics
{
    public class MetricTrackerTests
    {
        #region GetOutcome

        [Theory]
        [InlineData(null, "Success")]
        [InlineData(typeof(CouchbaseException), "Error")]
        [InlineData(typeof(InvalidOperationException), "Error")] // Non-Couchbase exception
        [InlineData(typeof(AmbiguousTimeoutException), "AmbiguousTimeout")]
        [InlineData(typeof(UnambiguousTimeoutException), "UnambiguousTimeout")]
        [InlineData(typeof(DocumentNotFoundException), "DocumentNotFound")]
        [InlineData(typeof(PathExistsException), "PathExists")]
        public void GetOutcome(Type errorType, string expected)
        {
            // Act

            var result = MetricTracker.GetOutcome(errorType);

            // Assert

            Assert.Equal(expected, result);
        }

        #endregion
    }
}
