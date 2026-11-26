using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
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

        // A bucket node of a config apply returns before its connect ends, and a refused bucket leaves the node and its
        // pool for the pool to retry.
        [Fact]
        public async Task CreateBucketNode_RefusedBucket_KeepsTheNodeAndItsPool()
        {
            using var context = CreateContext();
            var initialized = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pool = FailingPool(new BucketNotFoundException("travel-sample"), initialized);
            var factory = CreateFactory(context, pool);
            var owner = Owner("travel-sample");

            using var node = factory.CreateBucketNode(_endPoint, KvAdapter(), owner);
            await initialized.Task.WaitAsync(Deadline);

            Assert.False(node.IsDead);
            Assert.Same(owner, node.Owner);
            Assert.Equal("travel-sample", ((ClusterNode)node).FixedBucketName);
            pool.Verify(p => p.Dispose(), Times.Never);
        }

        [Fact]
        public async Task CreateBucketNode_OtherError_KeepsTheNodeAndItsPool()
        {
            using var context = CreateContext();
            var initialized = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pool = FailingPool(new AuthenticationFailureException("Bad credentials."), initialized);
            var factory = CreateFactory(context, pool);

            using var node = factory.CreateBucketNode(_endPoint, KvAdapter(), Owner("travel-sample"));
            await initialized.Task.WaitAsync(Deadline);

            Assert.False(node.IsDead);
            pool.Verify(p => p.Dispose(), Times.Never);
        }

        // A connection reads the owner when the server pushes a config. The first connect starts inside the call, so
        // the owner must be set before it.
        [Fact]
        public void CreateBucketNode_SetsTheOwnerBeforeTheFirstConnect()
        {
            using var context = CreateContext();
            ClusterNode created = null;
            IBucket ownerAtConnect = null;
            var connects = 0;
            var pool = new Mock<IConnectionPool>();
            pool.Setup(p => p.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    connects++;
                    ownerAtConnect = created!.Owner;
                    return new TaskCompletionSource<bool>().Task;
                });
            var connectionPoolFactory = new Mock<IConnectionPoolFactory>();
            connectionPoolFactory.Setup(m => m.Create(It.IsAny<ClusterNode>()))
                .Returns((ClusterNode node) =>
                {
                    created = node;
                    return pool.Object;
                });
            var factory = CreateFactory(context, connectionPoolFactory.Object);
            var owner = Owner("travel-sample");

            using var node = factory.CreateBucketNode(_endPoint, KvAdapter(), owner);

            Assert.Equal(1, connects);
            Assert.Same(owner, ownerAtConnect);
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

        [Fact]
        public async Task BucketNode_OtherError_DisposesTheNodeAndThrows()
        {
            using var context = CreateContext();
            var error = new AuthenticationFailureException("Bad credentials.");
            var pool = FailingPool(error);
            var factory = CreateFactory(context, pool);

            var thrown = await Assert.ThrowsAsync<AuthenticationFailureException>(() => factory
                .CreateAndConnectAsync(_endPoint, null, "travel-sample").WaitAsync(Deadline));

            Assert.Same(error, thrown);
            pool.Verify(p => p.Dispose(), Times.Once);
        }

        private static IBucket Owner(string name) => Mock.Of<IBucket>(b => b.Name == name);

        private static NodeAdapter KvAdapter()
        {
            var config = new BucketConfig
            {
                Name = BucketConfig.GlobalBucketName,
                NodesExt = [new NodesExt { Hostname = "localhost", Services = new Services { Kv = 11210 } }]
            };
            config.OnDeserialized();
            return config.GetClusterNodes()[0];
        }

        private static ClusterContext CreateContext() =>
            new(new CancellationTokenSource(), new ClusterOptions().WithPasswordAuthentication("username", "password"));

        private static Mock<IConnectionPool> FailingPool(Exception error, TaskCompletionSource<bool> initialized = null)
        {
            var pool = new Mock<IConnectionPool>();
            pool.Setup(p => p.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    initialized?.TrySetResult(true);
                    return Task.FromException(error);
                });
            return pool;
        }

        private ClusterNodeFactory CreateFactory(ClusterContext context, Mock<IConnectionPool> pool)
        {
            var connectionPoolFactory = new Mock<IConnectionPoolFactory>();
            connectionPoolFactory.Setup(m => m.Create(It.IsAny<ClusterNode>())).Returns(pool.Object);
            return CreateFactory(context, connectionPoolFactory.Object);
        }

        private ClusterNodeFactory CreateFactory(ClusterContext context, IConnectionPoolFactory connectionPoolFactory)
        {
            return new ClusterNodeFactory(context, connectionPoolFactory,
                new Logger<ClusterNode>(new TestOutputLoggerFactory(_outputHelper)),
                new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                new Mock<ISaslMechanismFactory>().Object, new Redactor(RedactionLevel.None),
                NoopRequestTracer.Instance, new Mock<IOperationConfigurator>().Object);
        }
    }
}
