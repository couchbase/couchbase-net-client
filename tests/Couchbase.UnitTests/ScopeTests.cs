using Couchbase.Services.KeyValue;
using Moq;
using Xunit;

namespace Couchbase.UnitTests
{
    public class ScopeTests
    {
        [Fact]
        public void Collection_Indexer_NotFound_Throws_CollectionMissingException()
        {
            var mockBucket = new Mock<IBucket>();
            var scope = new Scope("_default", "0", new ICollection[]{}, mockBucket.Object);

            Assert.Throws<CollectionMissingException>(() =>
            {
                var collection = scope["doesnotexist"];
            });
        }

        [Fact]
        public void Collection_NotFound_Throws_CollectionMissingException()
        {
            var mockBucket = new Mock<IBucket>();
            var scope = new Scope("_default", "0", new ICollection[]{}, mockBucket.Object);

            Assert.Throws<CollectionMissingException>(() =>
            {
                var collection = scope.Collection("doesnotexist");
            });
        }
    }
}
