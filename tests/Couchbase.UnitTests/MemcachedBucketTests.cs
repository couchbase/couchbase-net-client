using System;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Xunit;

namespace Couchbase.UnitTests
{
    public class MemcachedBucketTests
    {
        [Fact]
        public void ViewIndexes_Throws_NotSupportedException()
        {
            var bucket = new MemcachedBucket("default", new Configuration(), new ConfigContext(new Configuration()));

            Assert.Throws<NotSupportedException>(() => bucket.ViewIndexes);
        }

        [Fact]
        public async Task ViewQueryAsync_Throws_NotSupportedException()
        {
            var bucket = new MemcachedBucket("default", new Configuration(), new ConfigContext(new Configuration()));

            await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                await bucket.ViewQueryAsync<dynamic>("designDoc", "viewName").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [Fact]
        public async Task Indexer_Throws_NotSupportedException_When_Name_Is_Not_Default()
        {
            var bucket = new MemcachedBucket("default", new Configuration(), new ConfigContext(new Configuration()));

            await Assert.ThrowsAsync<NotSupportedException>(async () => { await bucket["xxxxx"].ConfigureAwait(false); }).ConfigureAwait(false);
        }

        [Fact(Skip = "Will be enabled in later commit.")]
        public async Task Indexer_Succeeds_When_Name_Is_Default()
        {
            var bucket = new MemcachedBucket("default", new Configuration(), new ConfigContext(new Configuration()));
            await bucket.Bootstrap().ConfigureAwait(false);

            var scope = await bucket[BucketBase.DefaultScope].ConfigureAwait(false);
            Assert.Equal(BucketBase.DefaultScope, scope.Name);
        }

        [Fact(Skip = "Will be enabled in later commit.")]
        public async Task ScopeAsync_Succeeds_When_Name_Is_Default()
        {
            var bucket = new MemcachedBucket("default", new Configuration(), new ConfigContext(new Configuration()));
            await bucket.Bootstrap().ConfigureAwait(false);

            var scope = await bucket.ScopeAsync(BucketBase.DefaultScope).ConfigureAwait(false);
            Assert.Equal(BucketBase.DefaultScope, scope.Name);
        }

        [Fact(Skip = "Will be enabled in later commit.")]
        public async Task ScopeAsync_Throws_NotSupportedException_When_Name_Is_Not_Default()
        {
            var bucket = new MemcachedBucket("default", new Configuration(), new ConfigContext(new Configuration()));
            await bucket.Bootstrap().ConfigureAwait(false);

            await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                await bucket.ScopeAsync("xxxx").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }
}
