using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;
using Xunit.Abstractions;
using TraceListener = Couchbase.Core.Diagnostics.Tracing.TraceListener;

#pragma warning disable CS8632
namespace Couchbase.UnitTests.Core
{
    public class ClusterContextTests
    {
        private readonly ITestOutputHelper _output;

        public ClusterContextTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(@"Documents\Configs\config-localhost-alt-addresses-8093.json", 8093)]
        [InlineData(@"Documents\Configs\config-localhost-alt-addresses-5555.json", 5555)]
        public void Use_Alternate_Address_Query_Port(string configPath, int expectedPort)
        {
            // Arrange

            var config = ResourceHelper.ReadResource(configPath, InternalSerializationContext.Default.BucketConfig);
            var options = new ClusterOptions
            {
                NetworkResolution = NetworkResolution.External
            };
            config.SetEffectiveNetworkResolution(options);

            var nodeAdapter = new NodeAdapter(null, config.NodesExt.First(), config);
            var clusterNode = CreateMockedNode("localhost", 11210, nodeAdapter);

            var context = new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password"));
            context.AddNode(clusterNode);

            // Act

            var serviceUriProvider = new ServiceUriProvider(context);
            var uri = serviceUriProvider.GetRandomQueryUri();

            //Assert

            Assert.Equal(expectedPort, uri.Port);
        }

        [Fact]
        public void PruneNodes_Removes_Rebalanced_Node()
        {
            //Arrange

            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-error.json",
                InternalSerializationContext.Default.BucketConfig);
            var context = new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password"));

            var hosts = new List<string>{"10.143.194.101", "10.143.194.102", "10.143.194.103", "10.143.194.104"};
            hosts.ForEach(x => context.AddNode(CreateMockedNode(x, 11210)));

            //Act

            context.PruneNodes(config);

            //Assert

            var removed = new HostEndpointWithPort("10.143.194.102", 11210);

            Assert.DoesNotContain(context.Nodes, node => node.EndPoint.Equals(removed));
        }

        [Fact]
        public async Task Bootstrap_Uses_Random_Seed_Nodes()
        {
            // set up a mock that records which node was chosen, then immediately faults
            ConcurrentDictionary<string, int> chosenNodes = new();
            var nodeFactoryMock = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
            nodeFactoryMock.Setup(f =>
                    f.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
                .Returns((HostEndpointWithPort host, CancellationToken token) =>
                {
                    chosenNodes.AddOrUpdate(host.Host,
                        addValueFactory: s => 0,
                        updateValueFactory: (s, count) => count + 1);
                    throw new CouchbaseException(new KeyValueErrorContext()
                    {
                        Status = ResponseStatus.BucketNotConnected
                    }, "break early");
                    // return Task.FromResult(CreateMockedNode(host.Host, host.Port));
                });
            var options = new ClusterOptions().WithConnectionString("couchbase://node1,node2,node3?random_seed_nodes=true").WithPasswordAuthentication("username", "password");
            options.EnableDnsSrvResolution = false;
            options.AddClusterService<IClusterNodeFactory>(nodeFactoryMock.Object);
            using var cts = new CancellationTokenSource();
            var context = new ClusterContext(cts, options);

            // call bootstrap enough times for random behavior to become apparent.
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    await context.BootstrapGlobalAsync();
                }
                catch
                { }
            }

            foreach (var kvp in chosenNodes)
            {
                _output.WriteLine($"{kvp.Key} = {kvp.Value}");
            }

            // if nodes are being chosen randomly, there should be more than one entry in the "chosenNodes" list.
            // before fixing the issue, only "node1" was chosen.
            Assert.NotEmpty(chosenNodes);
            Assert.InRange(chosenNodes.Count, 2, 3);
        }

        #region Dispose

        /// <summary>
        /// Dispose() used to check-then-set the `_disposed` flag as a plain, unsynchronized bool, so
        /// concurrent callers could all observe "not yet disposed" before any of them recorded that
        /// they had disposed it, and each would run the full disposal body. The window is between the
        /// read and the write of the flag, before anything injectable runs, so this can only assert
        /// that repeated calls are idempotent - not exercise the actual race, which isn't reliably
        /// reproducible in a unit test. That's what the comment on the Interlocked guard is for.
        /// </summary>
        [Fact]
        public void Dispose_CalledMultipleTimes_DisposesConfigHandlerOnce()
        {
            var mockConfigHandler = new Mock<IConfigHandler>();
            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            options.AddClusterService<IConfigHandler>(mockConfigHandler.Object);

            var context = new ClusterContext(null, new CancellationTokenSource(), options);

            context.Dispose();
            context.Dispose();

            mockConfigHandler.Verify(x => x.Dispose(), Times.Once);
        }

        /// <summary>
        /// _disposed is latched atomically before any teardown runs, so if Cancel() itself throws
        /// (e.g. a callback registered on this token by unrelated code throws), the rest of Dispose()
        /// - configHandler, semaphore, tokenSource, owned objects, buckets, nodes - must still run.
        /// Without a guard around Cancel(), that throw would propagate out of Dispose() and skip
        /// everything after it permanently, since a retried Dispose() call just returns immediately.
        /// </summary>
        [Fact]
        public void Dispose_TokenCancellationCallbackThrows_StillDisposesTheRestOfTeardown()
        {
            var mockConfigHandler = new Mock<IConfigHandler>();
            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            options.AddClusterService<IConfigHandler>(mockConfigHandler.Object);

            var tokenSource = new CancellationTokenSource();
            tokenSource.Token.Register(() => throw new InvalidOperationException("unrelated callback failure"));

            var context = new ClusterContext(tokenSource, options);

            var ex = Record.Exception(() => context.Dispose());

            Assert.Null(ex);
            mockConfigHandler.Verify(x => x.Dispose(), Times.Once);
        }

        #endregion

        #region Dispose vs. in-flight bucket open

        /// <summary>
        /// GetOrCreateBucketLockedAsync holds _semaphore for the duration of the bootstrap attempt.
        /// If Dispose() runs concurrently and disposes the semaphore before this call gets back to
        /// releasing it, the bare _semaphore.Release() in its finally block used to throw
        /// ObjectDisposedException - discarding whatever the bootstrap attempt actually produced and
        /// replacing it with an unrelated, confusing exception.
        /// </summary>
        [Fact]
        public async Task GetOrCreateBucketLockedAsync_DisposedWhileInFlight_ThrowsCancellationInsteadOfObjectDisposedException()
        {
            // Completing connectGate is deliberately deferred until after Dispose() has returned
            // (see below), rather than done from this callback. RunContinuationsAsynchronously only
            // keeps its continuation off Cancel()'s call stack - it does not guarantee that
            // continuation loses the race against the rest of Dispose(). Only recording that
            // cancellation happened here, and completing connectGate afterwards, makes "the semaphore
            // is already disposed before Release() is attempted" a sequenced fact instead of a race.
            var connectGate = new TaskCompletionSource<IClusterNode>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellationObserved = false;
            var reachedGate = new AsyncCounter();
            var nodeFactoryMock = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
            nodeFactoryMock.Setup(f => f.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
                .Returns((HostEndpointWithPort _, CancellationToken token) =>
                {
                    token.Register(() => cancellationObserved = true);
                    reachedGate.Increment();
                    return connectGate.Task;
                });

            var options = new ClusterOptions().WithConnectionString("couchbase://node1").WithPasswordAuthentication("username", "password");
            options.EnableDnsSrvResolution = false;
            options.AddClusterService<IClusterNodeFactory>(nodeFactoryMock.Object);

            var context = new ClusterContext(new CancellationTokenSource(), options);

            // Starts GetOrCreateBucketLockedAsync, which acquires _semaphore and blocks inside
            // CreateAndConnectAsync - simulating a bucket open still in flight when shutdown begins.
            var bucketTask = context.GetOrCreateBucketAsync("default").AsTask();

            // Waits for the call to actually be inside CreateAndConnectAsync with its cancellation
            // callback registered, rather than guessing at how long that takes.
            await reachedGate.WaitForAsync(1);

            context.Dispose();

            // Dispose() is synchronous and Cancel() runs registrations synchronously, so by the time
            // Dispose() returns here, cancellation has already been observed and _semaphore has
            // already been disposed - deterministically, not a race with the continuation below.
            Assert.True(cancellationObserved);

            // Only now let the blocked bootstrap attempt unblock and reach its finally block, with
            // the semaphore's disposal already guaranteed to have happened first.
            connectGate.TrySetCanceled();

            // If a regression drops the Cancel() call in Dispose(), bucketTask never completes and
            // this hangs. That is deliberate: CI runs with --blame-hang, which turns a hang into a
            // named failure plus a dump, whereas a wall-clock bound turns a slow agent into one.
            var ex = await Record.ExceptionAsync(() => bucketTask);

            // connectGate is deterministically cancelled above, so assert that outcome directly
            // rather than merely "not ObjectDisposedException" - which would also pass for an
            // unrelated failure that never actually preserved the cancellation result.
            Assert.IsType<TaskCanceledException>(ex);
        }

        /// <summary>
        /// Cancellation is cooperative, and several of the awaits inside
        /// CreateAndBootStrapBucketAsync (SelectBucketAsync, GetClusterMap, BootstrapAsync) don't
        /// observe the token Dispose() cancels. So a bootstrap can still complete successfully after
        /// Dispose() has already drained Buckets - without a guard, GetOrCreateBucketLockedAsync would
        /// hand back a "successfully bootstrapped" bucket owned by an already-disposed context, and
        /// nothing would ever dispose it.
        /// </summary>
        [Fact]
        public async Task GetOrCreateBucketLockedAsync_BootstrapCompletesAfterDispose_DisposesLateBucketInsteadOfReturningIt()
        {
            // config-error.json has a matching Nodes + NodesExt entry for 10.143.194.101, and already
            // carries "cccp" in bucketCapabilities and a non-null vBucketServerMap.
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-error.json",
                InternalSerializationContext.Default.BucketConfig);

            var endpoint = new HostEndpointWithPort("10.143.194.101", 11210);

            var mockNode = new Mock<IClusterNode>();
            mockNode.SetupGet(n => n.EndPoint).Returns(endpoint);
            mockNode.SetupGet(n => n.IsAssigned).Returns(false);
            mockNode.Setup(n => n.SelectBucketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mockNode.Setup(n => n.GetClusterMap(null, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(config));

            var mockConfigHandler = new Mock<IConfigHandler>();

            // BootstrapAsync never observes cancellation, simulating the real, cancellation-unaware
            // awaits noted above.
            var bootstrapGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reachedBootstrap = new AsyncCounter();
            LateBucket lateBucket = null;
            var bucketFactoryMock = new Mock<IBucketFactory>();
            ClusterContext context = null;
            bucketFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<BucketType>(), It.IsAny<BucketConfig>()))
                .Returns((string name, BucketType _, BucketConfig _) =>
                {
                    lateBucket = new LateBucket(name, context, bootstrapGate.Task, reachedBootstrap);
                    return lateBucket;
                });

            var options = new ClusterOptions().WithConnectionString("couchbase://10.143.194.101").WithPasswordAuthentication("username", "password");
            options.EnableDnsSrvResolution = false;
            options.AddClusterService<IBucketFactory>(bucketFactoryMock.Object);
            options.AddClusterService<IConfigHandler>(mockConfigHandler.Object);

            context = new ClusterContext(new CancellationTokenSource(), options);
            context.AddNode(mockNode.Object);

            // Starts the bootstrap, which reaches BootstrapAsync and blocks there - simulating a
            // bucket open still in flight, past the point of no return, when shutdown begins.
            var bucketTask = context.GetOrCreateBucketAsync("default").AsTask();

            // Waits for the bootstrap to actually be parked inside BootstrapAsync.
            await reachedBootstrap.WaitForAsync(1);

            context.Dispose();

            // Let the "late" bootstrap complete now that the context is already disposed.
            bootstrapGate.SetResult(true);

            var ex = await Record.ExceptionAsync(() => bucketTask);

            Assert.IsType<ObjectDisposedException>(ex);
            Assert.NotNull(lateBucket);
            Assert.True(lateBucket.DisposeCalled);

            // RegisterBucket subscribed the late bucket to _configHandler before we ever knew the
            // context was disposed; DisposeAsync() must unsubscribe it too, or the disposed bucket
            // stays referenced by the subscriber list forever.
            mockConfigHandler.Verify(x => x.Subscribe(lateBucket), Times.Once);
            mockConfigHandler.Verify(x => x.Unsubscribe(lateBucket), Times.Once);
        }

        /// <summary>
        /// The disposed-check used to sit only inside the `IsBootstrapped: true` branch. The real
        /// CouchbaseBucket.BootstrapAsync captures its own exceptions and returns normally instead of
        /// throwing, so a bootstrap that fails this way (IsBootstrapped false, no exception) skipped
        /// the check entirely: the loop moved on to a second endpoint, whose first move touched the
        /// already-disposed _tokenSource and threw an unrelated, confusing ObjectDisposedException -
        /// or, with only one endpoint, silently discarded the real failure behind a
        /// BucketNotFoundException - while the failed bucket itself was never disposed.
        /// </summary>
        [Fact]
        public async Task GetOrCreateBucketLockedAsync_FirstBootstrapFailsSoftlyAfterDispose_DisposesBucketWithoutRetryingSecondEndpoint()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-error.json",
                InternalSerializationContext.Default.BucketConfig);

            var endpoint1 = new HostEndpointWithPort("10.143.194.101", 11210);

            var mockNode1 = new Mock<IClusterNode>();
            mockNode1.SetupGet(n => n.EndPoint).Returns(endpoint1);
            mockNode1.SetupGet(n => n.IsAssigned).Returns(false);
            mockNode1.Setup(n => n.SelectBucketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mockNode1.Setup(n => n.GetClusterMap(null, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(config));

            // The gate lets BootstrapAsync's completion land after Dispose() has already run, without
            // needing a real race - bootstrapSucceeds: false makes it "fail softly" the way the real
            // CouchbaseBucket.BootstrapAsync does.
            var bootstrapGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reachedBootstrap = new AsyncCounter();
            LateBucket lateBucket = null;
            var bucketFactoryMock = new Mock<IBucketFactory>();
            ClusterContext context = null;
            bucketFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<BucketType>(), It.IsAny<BucketConfig>()))
                .Returns((string name, BucketType _, BucketConfig _) =>
                {
                    lateBucket = new LateBucket(name, context, bootstrapGate.Task, reachedBootstrap, bootstrapSucceeds: false);
                    return lateBucket;
                });

            var secondEndpointCalled = false;
            var nodeFactoryMock = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
            nodeFactoryMock.Setup(f => f.CreateAndConnectAsync(new HostEndpointWithPort("10.143.194.103", 11210), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    secondEndpointCalled = true;
                    return Task.FromException<IClusterNode>(new InvalidOperationException("second endpoint should never be attempted"));
                });

            var options = new ClusterOptions().WithConnectionString("couchbase://10.143.194.101,10.143.194.103?random_seed_nodes=false").WithPasswordAuthentication("username", "password");
            options.EnableDnsSrvResolution = false;
            options.AddClusterService<IBucketFactory>(bucketFactoryMock.Object);
            options.AddClusterService<IClusterNodeFactory>(nodeFactoryMock.Object);

            context = new ClusterContext(new CancellationTokenSource(), options);
            context.AddNode(mockNode1.Object);

            // Starts the bootstrap against the first endpoint, which reaches BootstrapAsync and
            // blocks there - simulating a bucket open still in flight when shutdown begins.
            var bucketTask = context.GetOrCreateBucketAsync("default").AsTask();

            // Waits for the bootstrap to actually be parked inside BootstrapAsync.
            await reachedBootstrap.WaitForAsync(1);

            context.Dispose();

            // Let the first endpoint's bootstrap finish - softly failed - now that the context is
            // already disposed.
            bootstrapGate.SetResult(true);

            var ex = await Record.ExceptionAsync(() => bucketTask);

            Assert.IsType<ObjectDisposedException>(ex);
            Assert.NotNull(lateBucket);
            Assert.True(lateBucket.DisposeCalled);
            Assert.False(secondEndpointCalled);
        }

        /// <summary>
        /// With more than one bootstrap endpoint, a disposal-triggered cancellation on the first
        /// endpoint must propagate immediately. Falling through to the generic retry handler would
        /// try a second, already-doomed endpoint - whose very first move (reading CancellationToken)
        /// throws ObjectDisposedException against the now-disposed _tokenSource, replacing the
        /// meaningful cancellation with the confusing exception this whole fix exists to avoid.
        /// </summary>
        [Fact]
        public async Task GetOrCreateBucketLockedAsync_DisposedDuringFirstEndpoint_DoesNotRetrySecondEndpoint()
        {
            var connectGate = new TaskCompletionSource<IClusterNode>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondEndpointCalled = false;
            var reachedFirstEndpoint = new AsyncCounter();

            var nodeFactoryMock = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
            nodeFactoryMock.Setup(f => f.CreateAndConnectAsync(new HostEndpointWithPort("node1", 11210), It.IsAny<CancellationToken>()))
                .Returns((HostEndpointWithPort _, CancellationToken token) =>
                {
                    token.Register(() => connectGate.TrySetCanceled(token));
                    reachedFirstEndpoint.Increment();
                    return connectGate.Task;
                });
            nodeFactoryMock.Setup(f => f.CreateAndConnectAsync(new HostEndpointWithPort("node2", 11210), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    secondEndpointCalled = true;
                    return Task.FromException<IClusterNode>(new InvalidOperationException("node2 should never be attempted"));
                });

            var options = new ClusterOptions().WithConnectionString("couchbase://node1,node2?random_seed_nodes=false").WithPasswordAuthentication("username", "password");
            options.EnableDnsSrvResolution = false;
            options.AddClusterService<IClusterNodeFactory>(nodeFactoryMock.Object);

            var context = new ClusterContext(new CancellationTokenSource(), options);

            var bucketTask = context.GetOrCreateBucketAsync("default").AsTask();

            // Waits for the first endpoint's connect attempt to actually be in flight.
            await reachedFirstEndpoint.WaitForAsync(1);

            context.Dispose();

            var ex = await Record.ExceptionAsync(() => bucketTask);

            Assert.IsType<TaskCanceledException>(ex);
            Assert.False(secondEndpointCalled);
        }

        /// <summary>
        /// The opposite case: an ObjectDisposedException unrelated to the ClusterContext's own
        /// disposal (e.g. some other, already-disposed resource on the first node) must not disable
        /// the existing per-endpoint fallback - the loop should still try the next endpoint.
        /// </summary>
        [Fact]
        public async Task GetOrCreateBucketLockedAsync_UnrelatedObjectDisposedExceptionOnFirstEndpoint_StillTriesSecondEndpoint()
        {
            var nodeFactoryMock = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
            nodeFactoryMock.Setup(f => f.CreateAndConnectAsync(new HostEndpointWithPort("node1", 11210), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ObjectDisposedException("unrelated-connection"));
            nodeFactoryMock.Setup(f => f.CreateAndConnectAsync(new HostEndpointWithPort("node2", 11210), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("node2 was attempted"));

            var options = new ClusterOptions().WithConnectionString("couchbase://node1,node2?random_seed_nodes=false").WithPasswordAuthentication("username", "password");
            options.EnableDnsSrvResolution = false;
            options.AddClusterService<IClusterNodeFactory>(nodeFactoryMock.Object);

            using var context = new ClusterContext(new CancellationTokenSource(), options);

            var ex = await Record.ExceptionAsync(() => context.GetOrCreateBucketAsync("default").AsTask());

            Assert.IsType<InvalidOperationException>(ex);
        }

        /// <summary>
        /// Minimal <see cref="BucketBase"/> whose BootstrapAsync completes only when told to, so a
        /// test can control exactly when a bootstrap - successful, or soft-failed via
        /// bootstrapSucceeds: false - lands relative to Dispose().
        /// </summary>
        private class LateBucket : BucketBase
        {
            private readonly Task _bootstrapGate;
            private readonly AsyncCounter _reachedBootstrap;
            private readonly bool _bootstrapSucceeds;

            public bool DisposeCalled { get; private set; }

            public LateBucket(string name, ClusterContext context, Task bootstrapGate, AsyncCounter reachedBootstrap, bool bootstrapSucceeds = true)
                : base(name, context,
                    new Mock<IScopeFactory>().Object,
                    new Mock<IRetryOrchestrator>().Object,
                    new Mock<ILogger>().Object,
                    new TypedRedactor(RedactionLevel.None),
                    new Mock<IBootstrapperFactory>().Object,
                    NoopRequestTracer.Instance,
                    new Mock<IOperationConfigurator>().Object,
                    new BestEffortRetryStrategy(),
                    new Mock<BucketConfig>().Object)
            {
                _bootstrapGate = bootstrapGate;
                _reachedBootstrap = reachedBootstrap;
                _bootstrapSucceeds = bootstrapSucceeds;
            }

            [Obsolete("The View service has been deprecated; overridden only to satisfy the base type.")]
            public override IViewIndexManager ViewIndexes => throw new NotImplementedException();

            public override ICouchbaseCollectionManager Collections => throw new NotImplementedException();

            public override IScope Scope(string scopeName) => throw new NotImplementedException();

            [Obsolete("The View service has been deprecated; overridden only to satisfy the base type.")]
            public override Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName, ViewOptions options = null) =>
                throw new NotImplementedException();

            public override Task ForceConfigUpdateAsync() => throw new NotImplementedException();

            internal override Task<ResponseStatus> SendAsync(IOperation op, CancellationTokenPair token = default) =>
                throw new NotImplementedException();

            // The real CouchbaseBucket.BootstrapAsync captures its own exceptions and returns
            // normally rather than throwing - _bootstrapSucceeds: false mirrors that "soft failure"
            // shape (IsBootstrapped ends up false, but nothing propagates as an exception).
            internal override async Task BootstrapAsync(IClusterNode bootstrapNode)
            {
                _reachedBootstrap.Increment();
                await _bootstrapGate.ConfigureAwait(false);
                if (!_bootstrapSucceeds)
                {
                    CaptureException(new InvalidOperationException("simulated soft bootstrap failure"));
                }
            }

            public override Task ConfigUpdatedAsync(BucketConfig newConfig) => Task.CompletedTask;

            public override void Dispose()
            {
                DisposeCalled = true;
                base.Dispose();
            }
        }

        #endregion

        #region Cluster-wide feature support

        /// <summary>
        /// SupportsPreserveTtl and SupportsBinaryXattr describe the cluster, but they used to be
        /// assigned from whichever node initialized last, at eight separate sites each overwriting
        /// the one before - so in a mixed-version cluster the answer depended on ordering. A feature
        /// is only safe to use if every node offers it.
        /// </summary>
        [Fact]
        public void Cluster_Feature_Support_Requires_Every_Node()
        {
            using var context = new ClusterContext(null,
                new ClusterOptions().WithPasswordAuthentication("username", "password"));

            //The restrictive node is added first and the permissive one last, deliberately: under
            //the old last-write-wins rule the permissive node would win and this would read true.
            context.AddNode(NodeSupporting("host1", ServerFeatures.PreserveTtl));
            context.AddNode(NodeSupporting("host2", ServerFeatures.PreserveTtl, ServerFeatures.SubdocBinaryXattr));

            Assert.True(context.SupportsPreserveTtl);
            Assert.False(context.SupportsBinaryXattr);
        }

        [Fact]
        public void Cluster_Feature_Support_When_Every_Node_Agrees()
        {
            using var context = new ClusterContext(null,
                new ClusterOptions().WithPasswordAuthentication("username", "password"));

            context.AddNode(NodeSupporting("host1", ServerFeatures.PreserveTtl, ServerFeatures.SubdocBinaryXattr));
            context.AddNode(NodeSupporting("host2", ServerFeatures.PreserveTtl, ServerFeatures.SubdocBinaryXattr));

            Assert.True(context.SupportsPreserveTtl);
            Assert.True(context.SupportsBinaryXattr);
        }

        /// <summary>
        /// Nothing is known before a node has negotiated, so nothing is claimed.
        /// </summary>
        [Fact]
        public void Cluster_Feature_Support_With_No_Nodes()
        {
            using var context = new ClusterContext(null,
                new ClusterOptions().WithPasswordAuthentication("username", "password"));

            Assert.False(context.SupportsPreserveTtl);
            Assert.False(context.SupportsBinaryXattr);
        }

        /// <summary>
        /// A node that has been added but has not negotiated yet is skipped rather than counted as
        /// unsupporting, so the flags do not flap to false while a node is coming up.
        /// </summary>
        [Fact]
        public void Cluster_Feature_Support_Ignores_Nodes_That_Have_Not_Negotiated()
        {
            using var context = new ClusterContext(null,
                new ClusterOptions().WithPasswordAuthentication("username", "password"));

            context.AddNode(NodeSupporting("host1", ServerFeatures.PreserveTtl));
            context.AddNode(Mock.Of<IClusterNode>(node =>
                node.ServerFeatures == null &&
                node.EndPoint == new HostEndpointWithPort("host2", 11210)));

            Assert.True(context.SupportsPreserveTtl);
        }

        /// <summary>
        /// And a lagging node leaving restores what the remaining cluster offers.
        /// </summary>
        [Fact]
        public void Cluster_Feature_Support_Recovers_When_A_Lagging_Node_Leaves()
        {
            using var context = new ClusterContext(null,
                new ClusterOptions().WithPasswordAuthentication("username", "password"));

            //Lagging node first, so the permissive node is the one last-write-wins would have
            //picked - the assertion below only means something with it in this order.
            var lagging = Mock.Of<IClusterNode>(node =>
                node.ServerFeatures == new ServerFeatureSet(Array.Empty<ServerFeatures>()) &&
                node.EndPoint == new HostEndpointWithPort("host1", 11210));
            context.AddNode(lagging);
            context.AddNode(NodeSupporting("host2", ServerFeatures.PreserveTtl));

            Assert.False(context.SupportsPreserveTtl);

            context.RemoveNode(lagging);

            Assert.True(context.SupportsPreserveTtl);
        }

        /// <summary>
        /// Cluster teardown clears the node list in bulk rather than removing nodes one at a time, so
        /// it has to recompute as well - otherwise the flags keep describing a cluster that is gone.
        /// </summary>
        [Fact]
        public void Cluster_Feature_Support_Is_Cleared_When_All_Nodes_Are_Removed()
        {
            using var context = new ClusterContext(null,
                new ClusterOptions().WithPasswordAuthentication("username", "password"));

            context.AddNode(NodeSupporting("host1", ServerFeatures.PreserveTtl));
            Assert.True(context.SupportsPreserveTtl);

            context.RemoveAllNodes();

            Assert.False(context.SupportsPreserveTtl);
        }

        /// <summary>
        /// Disposing one bucket leaves the other buckets' nodes in place, so the flags must be
        /// recomputed from what remains rather than simply cleared.
        /// </summary>
        [Fact]
        public void Cluster_Feature_Support_Recomputes_When_One_Bucket_Is_Removed()
        {
            using var context = new ClusterContext(null,
                new ClusterOptions().WithPasswordAuthentication("username", "password"));

            var disposedBucket = Mock.Of<IBucket>(bucket => bucket.Name == "restrictive");

            //The restrictive node belongs to the bucket going away, so its departure should restore
            //the feature the remaining node offers.
            var restrictive = NodeSupporting("host1");
            restrictive.Owner = disposedBucket;
            context.AddNode(restrictive);
            context.AddNode(NodeSupporting("host2", ServerFeatures.PreserveTtl));

            Assert.False(context.SupportsPreserveTtl);

            context.RemoveAllNodes(disposedBucket);

            Assert.True(context.SupportsPreserveTtl);
        }

        /// <summary>
        /// Endpoints must differ: ClusterNodeList.Remove matches on EndPoint and BucketName, so nodes
        /// sharing a default endpoint are indistinguishable and removing one removes both.
        /// </summary>
        private static IClusterNode NodeSupporting(string host, params ServerFeatures[] features) =>
            Mock.Of<IClusterNode>(node =>
                node.ServerFeatures == new ServerFeatureSet(features) &&
                node.EndPoint == new HostEndpointWithPort(host, 11210));

        #endregion

        private IClusterNode CreateMockedNode(string hostname, int port, NodeAdapter nodeAdapter = null)
        {
            var mockConnectionPool = new Mock<IConnectionPool>();

            var mockConnectionPoolFactory = new Mock<IConnectionPoolFactory>();
            mockConnectionPoolFactory
                .Setup(m => m.Create(It.IsAny<ClusterNode>()))
                .Returns(mockConnectionPool.Object);

            nodeAdapter ??= new NodeAdapter
            {
                Hostname = hostname,
                KeyValue = port
            };

            var clusterNode = new ClusterNode(new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password")), mockConnectionPoolFactory.Object,
                new Mock<ILogger<ClusterNode>>().Object,
                new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                new Mock<ICircuitBreaker>().Object,
                new Mock<ISaslMechanismFactory>().Object,
                new TypedRedactor(RedactionLevel.None),
                new HostEndpointWithPort(hostname, port),
                nodeAdapter,
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object
            )
            {
                Owner = new FakeBucket("default", new ClusterOptions().WithPasswordAuthentication("username", "password"))
            };

            return clusterNode;
        }

        #region Tracing

        [Fact]
        public void When_Tracing_Disabled_Custom_To_CustomTraceListener()
        {
            using var listener = new CustomTraceListener();

            var options = new ClusterOptions { TracingOptions = { Enabled = false } };
            options.WithThresholdTracing(new ThresholdOptions
            {
                Enabled = false,
                ThresholdListener = listener
            }).WithOrphanTracing(options => options.Enabled = false);

            var services = options.BuildServiceProvider();
            var noopRequestTracer = services.GetService(typeof(IRequestTracer));

            Assert.IsAssignableFrom<NoopRequestTracer>(noopRequestTracer);
        }

        [Fact]
        public async Task BootstrapGlobal_Should_Not_Swallow_AuthenticationFailure()
        {
            var options = new ClusterOptions().WithConnectionString("couchbases://localhost1,localhost2").WithPasswordAuthentication("username", "password");
            var mockNodeFactory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
            mockNodeFactory.Setup(cnf => cnf.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
                .Throws(new AuthenticationFailureException());
            options.AddClusterService(mockNodeFactory.Object);
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(), options);
            var ex = await Assert.ThrowsAsync<AuthenticationFailureException>(() => context.BootstrapGlobalAsync());
        }


        [Fact]
        public async Task BootstrapGlobal_Should_Continue_After_AuthenticationFailureException()
        {
            var options = new ClusterOptions().WithConnectionString("couchbase://localhost1,localhost2?random_seed_nodes=false").WithPasswordAuthentication("username", "password");

            var mockNodeFactory = new Mock<IClusterNodeFactory>(MockBehavior.Loose);

            mockNodeFactory.Setup(cnf => cnf.CreateAndConnectAsync(new HostEndpointWithPort("localhost1", 11210), It.IsAny<CancellationToken>()))
                .Throws(new AuthenticationFailureException());

            var config = ResourceHelper.ReadResource(@"Documents\Configs\cluster-level-config-rev69.json",
                InternalSerializationContext.Default.BucketConfig);

            config.VBucketServerMap = new Couchbase.Core.Sharding.VBucketServerMapDto();

            var mockClusterNode = new Mock<IClusterNode>();
            mockClusterNode.Setup(cn => cn.GetClusterMap(null, It.IsAny<CancellationToken>())).Returns(Task.FromResult(config));

            mockNodeFactory.Setup(cnf => cnf.CreateAndConnectAsync(new HostEndpointWithPort("localhost2", 11210), It.IsAny<CancellationToken>()))
               .Returns(Task.FromResult(mockClusterNode.Object));

            options.AddClusterService(mockNodeFactory.Object);
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(), options);
            await context.BootstrapGlobalAsync();
        }

        [Fact]
        public void When_Tracing_Enabled_Custom_To_CustomTraceListener()
        {
            using var listener = new CustomTraceListener();

            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            options.WithThresholdTracing(new ThresholdOptions
            {
                Enabled = true,
                ThresholdListener = listener
            });

            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(), options);
            context.Start();

            var tracer = context.ServiceProvider.GetRequiredService<IRequestTracer>();
            var span = tracer.RequestSpan("works");
            span.Dispose();

            var activities = listener.GetActivities().Where(x => x.OperationName == "works").ToArray();

            foreach (var activity in activities)
            {
                _output.WriteLine($"The name of the activity is '{activity.DisplayName}'");
            }
            Assert.Single(activities);
        }

        [Fact]
        public void When_Tracing_Enabled_Custom_To_CustomTraceListener_Not_Disposed()
        {
            using var listener = new CustomTraceListener();

            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            options.WithThresholdTracing(new ThresholdOptions
            {
                Enabled = true,
                ThresholdListener = listener
            });

            using (var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(), options))
            {
                context.Start();

                var tracer = context.ServiceProvider.GetRequiredService<IRequestTracer>();
                var span = tracer.RequestSpan("works");
                span.Dispose();
            }

            Assert.False(listener.Disposed);
        }

        public class CustomTraceListener : TraceListener
        {
            public bool Disposed { get; private set; }

            public CustomTraceListener()
            {
                Start();
            }

            // Due to thread sync issues, a listener may receive the same activity more than once.
            // We use a hash set to avoid tracking it multiple times and breaking tests.
            private HashSet<Activity> _activities = new();

            public sealed override void Start()
            {
                Listener.ActivityStopped = activity =>
                {
                    // We may be receiving activities from other tests, so lock
                    lock (_activities)
                    {
                        _activities.Add(activity);
                    }
                };
                Listener.SampleUsingParentId = (ref ActivityCreationOptions<string> activityOptions) =>
                    ActivitySamplingResult.AllData;
                Listener.Sample = (ref ActivityCreationOptions<ActivityContext> activityOptions) =>
                    ActivitySamplingResult.AllData;
                Listener.ShouldListenTo = s => true;
            }

            public Activity[] GetActivities()
            {
                lock (_activities)
                {
                    return _activities.ToArray();
                }
            }

            public override void Dispose()
            {
                base.Dispose();
                Disposed = true;
            }
        }

        public class CustomRequestTracer : IRequestTracer
        {
            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public IRequestSpan RequestSpan(string name, IRequestSpan parentSpan = null)
            {
                return new CustomRequestSpan();
            }

            public IRequestTracer Start(TraceListener listener)
            {
                return new CustomRequestTracer();
            }
        }

        public class CustomRequestSpan : IRequestSpan
        {
            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public IRequestSpan SetAttribute(string key, bool value)
            {
                throw new NotImplementedException();
            }

            public IRequestSpan SetAttribute(string key, string value)
            {
                throw new NotImplementedException();
            }

            public IRequestSpan SetAttribute(string key, uint value)
            {
                throw new NotImplementedException();
            }

            public IRequestSpan AddEvent(string name, DateTimeOffset? timestamp = null)
            {
                throw new NotImplementedException();
            }

            public void End()
            {
                throw new NotImplementedException();
            }

            public IRequestSpan? Parent { get; set; }
            public IRequestSpan ChildSpan(string name)
            {
                throw new NotImplementedException();
            }

            public bool CanWrite { get; }
            public string? Id { get; }
            public uint? Duration { get; }

            public IRequestSpan SetStatus(RequestSpanStatusCode code)
            {
                throw new NotImplementedException();
            }
        }

        #endregion
    }
}
