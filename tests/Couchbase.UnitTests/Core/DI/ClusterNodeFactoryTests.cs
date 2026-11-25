using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Test.Common.Utils;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;
using Xunit.Abstractions;
using BucketNotFoundException = Couchbase.Management.Buckets.BucketNotFoundException;

namespace Couchbase.UnitTests.Core.DI
{
    public class ClusterNodeFactoryTests
    {
        // Bounds each wait, so a regression fails instead of hanging.
        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

        private readonly ITestOutputHelper _outputHelper;
        private readonly HostEndpointWithPort _endPoint = new("localhost", 11210);

        public ClusterNodeFactoryTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task BucketNode_RefusedBucketAccepted_ReturnsTheNodeAndKeepsItsPool()
        {
            using var context = CreateContext();
            var pool = FailingPool(new BucketNotFoundException("travel-sample"));
            var factory = CreateFactory(context, pool);

            var node = await factory.CreateAndConnectAsync(_endPoint, null, "travel-sample", acceptRefusedBucket: true)
                .WaitAsync(Deadline);

            Assert.False(node.IsDead);
            Assert.Equal("travel-sample", ((ClusterNode)node).FixedBucketName);
            pool.Verify(p => p.Dispose(), Times.Never);
            node.Dispose();
        }

        [Fact]
        public async Task BucketNode_RefusedBucketNotAccepted_DisposesTheNodeAndThrows()
        {
            using var context = CreateContext();
            var error = new BucketNotFoundException("travel-sample");
            var pool = FailingPool(error);
            var factory = CreateFactory(context, pool);

            var thrown = await Assert.ThrowsAsync<BucketNotFoundException>(() => factory
                .CreateAndConnectAsync(_endPoint, null, "travel-sample", acceptRefusedBucket: false).WaitAsync(Deadline));

            Assert.Same(error, thrown);
            pool.Verify(p => p.Dispose(), Times.Once);
        }

        [Fact]
        public async Task BucketNode_RefusedBucketWithTheDefaultOverload_DisposesTheNodeAndThrows()
        {
            using var context = CreateContext();
            var error = new BucketNotFoundException("travel-sample");
            var pool = FailingPool(error);
            var factory = CreateFactory(context, pool);

            var thrown = await Assert.ThrowsAsync<BucketNotFoundException>(() => factory
                .CreateAndConnectAsync(_endPoint, null, "travel-sample").WaitAsync(Deadline));

            Assert.Same(error, thrown);
            pool.Verify(p => p.Dispose(), Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task BucketNode_OtherError_DisposesTheNodeAndThrows(bool acceptRefusedBucket)
        {
            using var context = CreateContext();
            var error = new AuthenticationFailureException("Bad credentials.");
            var pool = FailingPool(error);
            var factory = CreateFactory(context, pool);

            var thrown = await Assert.ThrowsAsync<AuthenticationFailureException>(() => factory
                .CreateAndConnectAsync(_endPoint, null, "travel-sample", acceptRefusedBucket).WaitAsync(Deadline));

            Assert.Same(error, thrown);
            pool.Verify(p => p.Dispose(), Times.Once);
        }

        private static ClusterContext CreateContext() =>
            new(new CancellationTokenSource(), new ClusterOptions().WithPasswordAuthentication("username", "password"));

        private static Mock<IConnectionPool> FailingPool(Exception error)
        {
            var pool = new Mock<IConnectionPool>();
            pool.Setup(p => p.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.FromException(error));
            return pool;
        }

        private ClusterNodeFactory CreateFactory(ClusterContext context, Mock<IConnectionPool> pool)
        {
            var connectionPoolFactory = new Mock<IConnectionPoolFactory>();
            connectionPoolFactory.Setup(m => m.Create(It.IsAny<ClusterNode>())).Returns(pool.Object);

            return new ClusterNodeFactory(context, connectionPoolFactory.Object,
                new Logger<ClusterNode>(new TestOutputLoggerFactory(_outputHelper)),
                new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                new Mock<ISaslMechanismFactory>().Object, new Redactor(RedactionLevel.None),
                NoopRequestTracer.Instance, new Mock<IOperationConfigurator>().Object);
        }
    }
}
