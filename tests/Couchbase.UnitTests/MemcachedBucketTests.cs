using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
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

            Assert.ThrowsAsync<NotSupportedException>(async () => await bucket.ScopeAsync("xxxxx"));
        }

        [Fact(Skip = "Will be enabled in later commit.")]
        public async Task Indexer_Succeeds_When_Name_Is_Default()
        {
            var localhost = HostEndpoint.Parse("10.112.192.102:8091");
            var bucketConfig = ResourceHelper.ReadResource<BucketConfig>("mycache.json");
            bucketConfig.Nodes.RemoveAt(1);

            var mockClusterNode = new Mock<IClusterNode>();
            mockClusterNode.Setup(x => x.EndPoint).Returns(new HostEndpointWithPort("127.0.0.1", 8091));
            mockClusterNode.Setup(x => x.SelectBucketAsync(It.IsAny<IBucket>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var mockHttpClusterMap = new Mock<HttpClusterMapBase>();
            mockHttpClusterMap.Setup(x =>
                x.GetClusterMapAsync("default", localhost, CancellationToken.None)).
                Returns(Task.FromResult(bucketConfig));

            var bucket = CreateMemcachedBucket();
            await bucket.BootstrapAsync(mockClusterNode.Object).ConfigureAwait(false);

            var scope = await bucket.ScopeAsync(Scope.DefaultScopeName);
            Assert.Equal(Scope.DefaultScopeName, scope.Name);
        }

        [Fact(Skip = "Will be enabled in later commit.")]
        public async Task ScopeAsync_Succeeds_When_Name_Is_Default()
        {
            var localhost = HostEndpoint.Parse("10.112.192.102:8091");
            var bucketConfig = ResourceHelper.ReadResource<BucketConfig>("mycache.json");
            bucketConfig.Nodes.RemoveAt(1);

            var mockClusterNode = new Mock<IClusterNode>();
            mockClusterNode.Setup(x => x.EndPoint).Returns(new HostEndpointWithPort("127.0.0.1", 8091));
            mockClusterNode.Setup(x => x.SelectBucketAsync(It.IsAny<IBucket>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

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
            var localhost = HostEndpoint.Parse("10.112.192.102:8091");
            var bucketConfig = ResourceHelper.ReadResource<BucketConfig>("mycache.json");
            bucketConfig.Nodes.RemoveAt(1);

            var mockClusterNode = new Mock<IClusterNode>();
            mockClusterNode.Setup(x => x.EndPoint).Returns(new HostEndpointWithPort("127.0.0.1", 8091));
            mockClusterNode.Setup(x => x.SelectBucketAsync(It.IsAny<IBucket>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var mockHttpClusterMap = new Mock<HttpClusterMapBase>();
            mockHttpClusterMap.Setup(x =>
                    x.GetClusterMapAsync("default", localhost, CancellationToken.None)).
                Returns(Task.FromResult(bucketConfig));

            var bucket = CreateMemcachedBucket();
            await bucket.BootstrapAsync(mockClusterNode.Object).ConfigureAwait(false);

            Assert.Throws<NotSupportedException>(() => bucket.Scope("xxxx"));
        }

        #region Helpers

        private static MemcachedBucket CreateMemcachedBucket()
        {
            return new MemcachedBucket("default",
                new ClusterContext(),
                new Mock<IScopeFactory>().Object,
                new Mock<IRetryOrchestrator>().Object,
                new Mock<IKetamaKeyMapperFactory>().Object,
                new Mock<ILogger<MemcachedBucket>>().Object,
                new Mock<IRedactor>().Object,
                new Mock<IBootstrapperFactory>().Object,
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object,
                new BestEffortRetryStrategy());
        }

        #endregion
    }
}
