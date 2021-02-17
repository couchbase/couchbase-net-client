using Couchbase.Core;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class ScopeTests
    {
        [Fact]
        public void Collection_Indexer_NotFound_Throws_CollectionMissingException()
        {
            var mockBucket = new Mock<BucketBase>();
            var scope = new Scope(null, Mock.Of<ICollectionFactory>(), mockBucket.Object,
                new Mock<ILogger<Scope>>().Object, new Mock<IRequestTracer>().Object, new Mock<IOperationConfigurator>().Object);

            Assert.Throws<CollectionNotFoundException>(() =>
            {
                var collection = scope["doesnotexist"];
            });
        }

        [Fact]
        public void Collection_NotFound_Throws_CollectionMissingException()
        {
            var mockBucket = new Mock<BucketBase>();
            var scope = new Scope(null, Mock.Of<ICollectionFactory>(), mockBucket.Object,
                new Mock<ILogger<Scope>>().Object, new Mock<IRequestTracer>().Object, new Mock<IOperationConfigurator>().Object);

            Assert.Throws<CollectionNotFoundException>(() =>
            {
                var collection = scope.Collection("doesnotexist");
            });
        }
    }
}
