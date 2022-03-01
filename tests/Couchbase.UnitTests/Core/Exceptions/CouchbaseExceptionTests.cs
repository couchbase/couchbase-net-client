using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Analytics;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.Exceptions.Search;
using Couchbase.Core.Exceptions.View;
using Couchbase.Management.Eventing;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Exceptions
{
    public class CouchbaseExceptionTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public CouchbaseExceptionTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Theory]
        [InlineData(typeof(KeyValueErrorContext))]
        [InlineData(typeof(AnalyticsErrorContext))]
        [InlineData(typeof(QueryErrorContext))]
        [InlineData(typeof(SearchErrorContext))]
        [InlineData(typeof(ViewContextError))]
        [InlineData(typeof(ManagementErrorContext))]
        [InlineData(typeof(EventingFunctionErrorContext))]
        public void ToString_WithErrorContext_Success(Type contextType)
        {
            // Arrange

            var context = (IErrorContext) Activator.CreateInstance(contextType)!;

            var ex = new CouchbaseException(context);

            // Act

            var result = ex.ToString();
            _testOutputHelper.WriteLine(result);

            // Assert

            Assert.NotNull(result);
            Assert.Contains("-Context Info-", result);
        }
    }
}
