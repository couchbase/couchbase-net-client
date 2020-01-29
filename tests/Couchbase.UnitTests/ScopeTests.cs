using Couchbase.Core;
using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ScopeTests
    {
        [Fact]
        public void Collection_Indexer_NotFound_Throws_CollectionMissingException()
        {
            var mockBucket = new Mock<BucketBase>();
            var scope = new Scope("_default", "0", new ICollection[]{}, mockBucket.Object,
                new Mock<ILogger<Scope>>().Object);

            Assert.Throws<CollectionNotFoundException>(() =>
            {
                var collection = scope["doesnotexist"];
            });
        }

        [Fact]
        public void Collection_NotFound_Throws_CollectionMissingException()
        {
            var mockBucket = new Mock<BucketBase>();
            var scope = new Scope("_default", "0", new ICollection[]{}, mockBucket.Object,
                new Mock<ILogger<Scope>>().Object);

            Assert.Throws<CollectionNotFoundException>(() =>
            {
                var collection = scope.Collection("doesnotexist");
            });
        }
    }
}
