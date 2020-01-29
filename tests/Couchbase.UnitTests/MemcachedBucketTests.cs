using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests
{
    public class MemcachedBucketTests
    {
        [Fact]
        public void ViewIndexes_Throws_NotSupportedException()
        {
            var bucket = CreateMemcachedBucket();

            Assert.Throws<NotSupportedException>(() => bucket.ViewIndexes);
        }

        [Fact]
        public async Task ViewQueryAsync_Throws_NotSupportedException()
        {
            var bucket = CreateMemcachedBucket();

            await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                await bucket.ViewQueryAsync<dynamic, dynamic>("designDoc", "viewName").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [Fact]
        public void Indexer_Throws_NotSupportedException_When_Name_Is_Not_Default()
        {
            var bucket = CreateMemcachedBucket();

            Assert.Throws<NotSupportedException>(() => bucket["xxxxx"]);
        }

        [Fact(Skip = "Will be enabled in later commit.")]
        public async Task Indexer_Succeeds_When_Name_Is_Default()
        {
            var localhost = new Uri("http://10.112.192.102:8091");
            var bucketConfig = ResourceHelper.ReadResource<BucketConfig>("mycache.json");
            bucketConfig.Nodes.RemoveAt(1);

            var mockClusterNode = new Mock<IClusterNode>();
            mockClusterNode.Setup(x => x.EndPoint).Returns(localhost.GetIpEndPoint(8091, false));
            mockClusterNode.Setup(x => x.BootstrapUri).Returns(localhost);
            mockClusterNode.Setup(x => x.SelectBucket("default")).Returns(Task.CompletedTask);

            var mockHttpClusterMap = new Mock<HttpClusterMapBase>();
            mockHttpClusterMap.Setup(x =>
                x.GetClusterMapAsync("default", localhost, CancellationToken.None)).
                Returns(Task.FromResult(bucketConfig));

            var bucket = CreateMemcachedBucket();
            await bucket.BootstrapAsync(mockClusterNode.Object).ConfigureAwait(false);

            var scope = bucket[Scope.DefaultScopeName];
            Assert.Equal(Scope.DefaultScopeName, scope.Name);
        }

        [Fact(Skip = "Will be enabled in later commit.")]
        public async Task ScopeAsync_Succeeds_When_Name_Is_Default()
        {
            var localhost = new Uri("http://10.112.192.102:8091");
            var bucketConfig = ResourceHelper.ReadResource<BucketConfig>("mycache.json");
            bucketConfig.Nodes.RemoveAt(1);

            var mockClusterNode = new Mock<IClusterNode>();
            mockClusterNode.Setup(x => x.EndPoint).Returns(localhost.GetIpEndPoint(8091, false));
            mockClusterNode.Setup(x => x.BootstrapUri).Returns(localhost);
            mockClusterNode.Setup(x => x.SelectBucket("default")).Returns(Task.CompletedTask);

            var mockHttpClusterMap = new Mock<HttpClusterMapBase>();
            mockHttpClusterMap.Setup(x =>
                    x.GetClusterMapAsync("default", localhost, CancellationToken.None)).
                Returns(Task.FromResult(bucketConfig));

            var bucket = CreateMemcachedBucket();
            await bucket.BootstrapAsync(mockClusterNode.Object).ConfigureAwait(false);

            var scope = bucket.Scope(Scope.DefaultScopeName);
            Assert.Equal(Scope.DefaultScopeName, scope.Name);
        }

        [Fact(Skip = "Will be enabled in later commit.")]
        public async Task ScopeAsync_Throws_NotSupportedException_When_Name_Is_Not_Default()
        {
            var localhost = new Uri("http://10.112.192.102:8091");
            var bucketConfig = ResourceHelper.ReadResource<BucketConfig>("mycache.json");
            bucketConfig.Nodes.RemoveAt(1);

            var mockClusterNode = new Mock<IClusterNode>();
            mockClusterNode.Setup(x => x.EndPoint).Returns(localhost.GetIpEndPoint(8091, false));
            mockClusterNode.Setup(x => x.BootstrapUri).Returns(localhost);
            mockClusterNode.Setup(x => x.SelectBucket("default")).Returns(Task.CompletedTask);

            var mockHttpClusterMap = new Mock<HttpClusterMapBase>();
            mockHttpClusterMap.Setup(x =>
                    x.GetClusterMapAsync("default", localhost, CancellationToken.None)).
                Returns(Task.FromResult(bucketConfig));

            var bucket = CreateMemcachedBucket();
            await bucket.BootstrapAsync(mockClusterNode.Object).ConfigureAwait(false);

            Assert.Throws<NotSupportedException>(() => bucket.Scope("xxxx"));
        }

        #region Helpers

        private static MemcachedBucket CreateMemcachedBucket() =>
            new MemcachedBucket("default",
                new ClusterContext(),
                new Mock<IScopeFactory>().Object,
                new Mock<IRetryOrchestrator>().Object,
                new Mock<ILogger<MemcachedBucket>>().Object);

        #endregion
    }
}
