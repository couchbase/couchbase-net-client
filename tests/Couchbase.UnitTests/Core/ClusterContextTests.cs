using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.Diagnostics;
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
using Microsoft.Extensions.Time.Testing;
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

        #region Config refresh failure

        // Bounds each wait on a background rebootstrap, so a regression fails instead of hanging.
        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task SignalConfigRefreshFailed_WhileARebootstrapRuns_DoesNothing()
        {
            var clock = new FakeTimeProvider();
            var lookups = new AsyncCounter();
            var records = new TaskCompletionSource<IEnumerable<HostEndpoint>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dnsResolver = new Mock<IDnsResolver>();
            dnsResolver.Setup(x => x.GetDnsSrvEntriesAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .Callback(() => lookups.Increment())
                .Returns(() => records.Task);
            var nodeFactory = new Mock<IClusterNodeFactory>();
            nodeFactory.Setup(x => x.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("No node in this test."));
            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            options.AddClusterService(dnsResolver.Object);
            options.AddClusterService(nodeFactory.Object);
            options.AddClusterService<TimeProvider>(clock);
            options.ConnectionStringValue = new ConnectionString(ConnectionString.Parse("couchbase://example.com"),
                [new HostEndpoint("node-0", 11210)], isDnsSrv: true, new Uri("couchbase://example.com"));
            using var context = new ClusterContext(new CancellationTokenSource(), options);

            context.SignalConfigRefreshFailed();
            var first = context.DnsSrvRebootstrap;
            await lookups.WaitForAsync(1).WaitAsync(Deadline);

            // Enough time passed, so only the rebootstrap that runs stops a new one.
            clock.Advance(ClusterContext.MinTimeBetweenDnsSrvRebootstraps);
            context.SignalConfigRefreshFailed();
            Assert.Same(first, context.DnsSrvRebootstrap);

            records.SetResult([]);
            await first.WaitAsync(Deadline);
            Assert.Equal(1, lookups.Count);

            // Once the rebootstrap ends, the next signal runs a new one.
            context.SignalConfigRefreshFailed();
            await context.DnsSrvRebootstrap.WaitAsync(Deadline);
            Assert.Equal(2, lookups.Count);
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
            SetupSeedOrBucketNode(nodeFactoryMock, null, (_, _, token) =>
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
            mockNode.Setup(n => n.GetClusterMap(null, null, It.IsAny<CancellationToken>()))
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
            mockNode1.Setup(n => n.GetClusterMap(null, null, It.IsAny<CancellationToken>()))
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
            SetupSeedOrBucketNode(nodeFactoryMock, new HostEndpointWithPort("10.143.194.103", 11210), (_, _, _) =>
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
            SetupSeedOrBucketNode(nodeFactoryMock, new HostEndpointWithPort("node1", 11210), (_, _, token) =>
            {
                token.Register(() => connectGate.TrySetCanceled(token));
                reachedFirstEndpoint.Increment();
                return connectGate.Task;
            });
            SetupSeedOrBucketNode(nodeFactoryMock, new HostEndpointWithPort("node2", 11210), (_, _, _) =>
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
            SetupSeedOrBucketNode(nodeFactoryMock, new HostEndpointWithPort("node1", 11210),
                (_, _, _) => Task.FromException<IClusterNode>(new ObjectDisposedException("unrelated-connection")));
            SetupSeedOrBucketNode(nodeFactoryMock, new HostEndpointWithPort("node2", 11210),
                (_, _, _) => Task.FromException<IClusterNode>(new InvalidOperationException("node2 was attempted")));

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

            public LateBucket(string name, ClusterContext context, Task bootstrapGate, AsyncCounter reachedBootstrap, bool bootstrapSucceeds = true,
                BucketType bucketType = BucketType.Couchbase)
                : base(name, context,
                    new Mock<IScopeFactory>().Object,
                    new Mock<IRetryOrchestrator>().Object,
                    new Mock<ILogger>().Object,
                    new Redactor(RedactionLevel.None),
                    new Mock<IBootstrapperFactory>().Object,
                    NoopRequestTracer.Instance,
                    new Mock<IOperationConfigurator>().Object,
                    new BestEffortRetryStrategy(),
                    new Mock<BucketConfig>().Object)
            {
                _bootstrapGate = bootstrapGate;
                _reachedBootstrap = reachedBootstrap;
                _bootstrapSucceeds = bootstrapSucceeds;
                BucketType = bucketType;
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

        #region Bucket open waits for a vBucket map

        // The clock counts sleeps by duration, so this differs from the 2.5s sleeps of the bucket's config refresher.
        private static readonly TimeSpan MapFetchInterval = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan MapWaitTimeout = TimeSpan.FromSeconds(1);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetOrCreateBucketAsync_FirstConfigsHaveNoMap_WaitsForTheMap(bool httpStreaming)
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1");
            var configs = new Queue<BucketConfig>([
                OneNodeBucketConfig("node1", map: false),
                OneNodeBucketConfig("node1", map: false),
                OneNodeBucketConfig("node1", map: true)
            ]);
            fixture.AddUnassignedNode("node1", configs.Dequeue, httpStreaming);

            var open = fixture.Context.GetOrCreateBucketAsync("default").AsTask();
            await clock.WaitForSleepAsync(MapFetchInterval).WaitAsync(Deadline);
            Assert.False(open.IsCompleted);
            clock.Advance(MapFetchInterval);
            await clock.WaitForSleepAsync(MapFetchInterval).WaitAsync(Deadline);
            clock.Advance(MapFetchInterval);
            var bucket = Assert.IsType<CouchbaseBucket>(await open.WaitAsync(Deadline));

            Assert.Empty(configs);
            Assert.NotNull(bucket.KeyMapper);
            fixture.ConfigHandler.Verify(x => x.Subscribe(bucket), Times.Once);
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_NoMapUntilTheDeadline_FailsAndLeavesNothingBehind()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1");
            var fetches = 0;
            var node = fixture.AddUnassignedNode("node1", () =>
            {
                fetches++;
                return OneNodeBucketConfig("node1", map: false);
            });

            var open = fixture.Context.GetOrCreateBucketAsync("default").AsTask();
            await clock.WaitForSleepAsync(MapFetchInterval).WaitAsync(Deadline);
            clock.Advance(MapWaitTimeout);

            await Assert.ThrowsAsync<BucketNotFoundException>(() => open.WaitAsync(Deadline));
            Assert.Equal(2, fetches);
            await AssertFailedBucketsDisposedAsync(fixture, failed: 1);
            Assert.Empty(fixture.Context.Nodes);
            node.Verify(x => x.Dispose(), Times.Once);
            fixture.ConfigHandler.Verify(x => x.Subscribe(It.IsAny<IConfigUpdateEventSink>()), Times.Never);
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_NoMapUntilTheDeadline_LogsAWarning()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1");
            fixture.AddUnassignedNode("node1", () => OneNodeBucketConfig("node1", map: false));

            var open = fixture.Context.GetOrCreateBucketAsync("default").AsTask();
            await clock.WaitForSleepAsync(MapFetchInterval).WaitAsync(Deadline);
            clock.Advance(MapWaitTimeout);

            await Assert.ThrowsAsync<BucketNotFoundException>(() => open.WaitAsync(Deadline));
            fixture.Logger.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("has no vBucket map after the KvTimeout")),
                null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_FirstNodeHasNoMapUntilTheDeadline_OpensOnTheNextNode()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1", "node2");
            var node1 = fixture.AddUnassignedNode("node1", () => OneNodeBucketConfig("node1", map: false));
            var node2 = fixture.SetupNewNode("node2", () => OneNodeBucketConfig("node2", map: true));

            var open = fixture.Context.GetOrCreateBucketAsync("default").AsTask();
            await clock.WaitForSleepAsync(MapFetchInterval).WaitAsync(Deadline);
            clock.Advance(MapWaitTimeout);
            var bucket = Assert.IsType<CouchbaseBucket>(await open.WaitAsync(Deadline));

            await AssertFailedBucketsDisposedAsync(fixture, failed: 1, opened: bucket);
            node1.Verify(x => x.Dispose(), Times.Once);
            Assert.Same(node2.Object, Assert.Single(fixture.Context.Nodes));
            Assert.NotNull(bucket.KeyMapper);
            fixture.ConfigHandler.Verify(x => x.Subscribe(It.IsAny<IConfigUpdateEventSink>()), Times.Once);
            fixture.ConfigHandler.Verify(x => x.Subscribe(bucket), Times.Once);
        }

        // A bucket config newer than the cluster config lists another node, as after a rebalance.
        [Fact]
        public async Task GetOrCreateBucketAsync_ViewNewerThanGlobalConfig_SeedsFromTheView()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1");
            var globalConfig = OneNodeBucketConfig("node1", map: false);
            globalConfig.Name = BucketConfig.GlobalBucketName;
            fixture.Context.GlobalConfig = globalConfig;
            var view = OneNodeBucketConfig("node2", map: true);
            view.Rev = 5;
            view.OnDeserialized();
            fixture.Context.UpdateClusterNodesConfig(view);
            var node1Asked = false;
            fixture.SetupFailedConnect("node1", () => node1Asked = true);
            var node2 = fixture.SetupNewNode("node2", () => OneNodeBucketConfig("node2", map: true));

            var bucket = Assert.IsType<CouchbaseBucket>(
                await fixture.Context.GetOrCreateBucketAsync("default").AsTask().WaitAsync(Deadline));

            Assert.False(node1Asked);
            Assert.Same(node2.Object, Assert.Single(bucket.Nodes));
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_NoNodeHasAMap_WaitsOncePerOpen()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1", "node2");
            var node1 = fixture.AddUnassignedNode("node1", () => OneNodeBucketConfig("node1", map: false));
            var node2Fetches = 0;
            var node2 = fixture.SetupNewNode("node2", () =>
            {
                node2Fetches++;
                return OneNodeBucketConfig("node2", map: false);
            });

            var open = fixture.Context.GetOrCreateBucketAsync("default").AsTask();
            await clock.WaitForSleepAsync(MapFetchInterval).WaitAsync(Deadline);
            clock.Advance(MapWaitTimeout);

            await Assert.ThrowsAsync<BucketNotFoundException>(() => open.WaitAsync(Deadline));
            Assert.Equal(1, node2Fetches);
            fixture.Logger.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("has no vBucket map after the KvTimeout")),
                null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Exactly(2));
            await AssertFailedBucketsDisposedAsync(fixture, failed: 2);
            Assert.Empty(fixture.Context.Nodes);
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_FirstHostFailsAfterTheKvTimeout_WaitsForTheMapOnTheNextHost()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1", "node2");
            fixture.SetupFailedConnect("node1", () => clock.Advance(TimeSpan.FromTicks(2 * MapWaitTimeout.Ticks)));
            var configs = new Queue<BucketConfig>([
                OneNodeBucketConfig("node2", map: false),
                OneNodeBucketConfig("node2", map: true)
            ]);
            var node2 = fixture.SetupNewNode("node2", configs.Dequeue);

            var open = fixture.Context.GetOrCreateBucketAsync("default").AsTask();
            await Task.WhenAny(clock.WaitForSleepAsync(MapFetchInterval), open).WaitAsync(Deadline);
            Assert.False(open.IsCompleted);
            clock.Advance(MapFetchInterval);
            var bucket = Assert.IsType<CouchbaseBucket>(await open.WaitAsync(Deadline));

            Assert.Empty(configs);
            Assert.Same(node2.Object, Assert.Single(bucket.Nodes));
            Assert.NotNull(bucket.KeyMapper);
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_PollIntervalAboveTheKvTimeout_WaitsOnlyTheKvTimeout()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1");
            fixture.Context.ClusterOptions.ConfigPollFloorInterval = TimeSpan.FromTicks(5 * MapWaitTimeout.Ticks);
            var fetches = 0;
            fixture.AddUnassignedNode("node1", () =>
            {
                fetches++;
                return OneNodeBucketConfig("node1", map: false);
            });

            var open = fixture.Context.GetOrCreateBucketAsync("default").AsTask();
            await clock.WaitForSleepAsync(MapWaitTimeout).WaitAsync(Deadline);
            clock.Advance(MapWaitTimeout);

            await Assert.ThrowsAsync<BucketNotFoundException>(() => open.WaitAsync(Deadline));
            Assert.Equal(2, fetches);
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_FetchFailsOnceWhileWaitingForAMap_OpensOnTheSameNode()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1");
            var fetches = 0;
            var node = fixture.AddUnassignedNode("node1", () => ++fetches switch
            {
                1 => OneNodeBucketConfig("node1", map: false),
                2 => throw new TimeoutException(),
                _ => OneNodeBucketConfig("node1", map: true)
            });

            var open = fixture.Context.GetOrCreateBucketAsync("default").AsTask();
            await clock.WaitForSleepAsync(MapFetchInterval).WaitAsync(Deadline);
            clock.Advance(MapFetchInterval);
            await clock.WaitForSleepAsync(MapFetchInterval).WaitAsync(Deadline);
            Assert.False(open.IsCompleted);
            clock.Advance(MapFetchInterval);
            var bucket = Assert.IsType<CouchbaseBucket>(await open.WaitAsync(Deadline));

            Assert.Equal(3, fetches);
            Assert.NotNull(bucket.KeyMapper);
            Assert.Same(node.Object, Assert.Single(bucket.Nodes));
            Assert.Same(node.Object, Assert.Single(fixture.Context.Nodes));
            node.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_FetchFailsUntilTheDeadline_FailsAndRemovesTheNode()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1");
            var fetches = 0;
            var node = fixture.AddUnassignedNode("node1", () => ++fetches == 1
                ? OneNodeBucketConfig("node1", map: false)
                : throw new TimeoutException());

            var open = fixture.Context.GetOrCreateBucketAsync("default").AsTask();
            await clock.WaitForSleepAsync(MapFetchInterval).WaitAsync(Deadline);
            clock.Advance(MapFetchInterval);
            await clock.WaitForSleepAsync(MapFetchInterval).WaitAsync(Deadline);
            clock.Advance(MapWaitTimeout);

            await Assert.ThrowsAsync<TimeoutException>(() => open.WaitAsync(Deadline));
            Assert.Equal(3, fetches);
            Assert.Empty(fixture.Bootstrappers);
            Assert.Empty(fixture.Context.Nodes);
            node.Verify(x => x.Dispose(), Times.AtLeastOnce);
            fixture.ConfigHandler.Verify(x => x.Subscribe(It.IsAny<IConfigUpdateEventSink>()), Times.Never);
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_DisposedWhileWaitingForAMap_StopsWaiting()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1");
            fixture.AddUnassignedNode("node1", () => OneNodeBucketConfig("node1", map: false));

            var open = fixture.Context.GetOrCreateBucketAsync("default").AsTask();
            await clock.WaitForSleepAsync(MapFetchInterval).WaitAsync(Deadline);
            fixture.Context.Dispose();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => open.WaitAsync(Deadline));
            Assert.Empty(fixture.Bootstrappers);
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_MemcachedBucket_DoesNotWaitForAMap()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1");
            fixture.AddUnassignedNode("node1", () => OneNodeBucketConfig("node1", map: false, memcached: true),
                httpStreaming: true);

            var open = fixture.Context.GetOrCreateBucketAsync("default").AsTask();

            await AssertCompleted(open);
            Assert.IsType<MemcachedBucket>(await open);
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_SelectFindsNoHealthyConnection_FetchesTheConfigOverHttp()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1");
            var node = fixture.AddUnassignedNode("node1", () => OneNodeBucketConfig("node1", map: true));
            node.Setup(x => x.SelectBucketAsync("default", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DocumentNotFoundException("Failed to select bucket default."));
            var httpFetches = 0;
            fixture.SetupHttpConfig("node1", () =>
            {
                httpFetches++;
                return OneNodeBucketConfig("node1", map: true);
            });

            var bucket = Assert.IsType<CouchbaseBucket>(await fixture.Context.GetOrCreateBucketAsync("default")
                .AsTask().WaitAsync(Deadline));

            Assert.Equal(1, httpFetches);
            Assert.Same(bucket, node.Object.Owner);
            Assert.Same(node.Object, Assert.Single(fixture.Context.Nodes));
            node.Verify(x => x.Dispose(), Times.Never);
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_SelectFailsWithAnotherError_OpensOnTheNextNode()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1", "node2");
            var node1 = fixture.AddUnassignedNode("node1", () => OneNodeBucketConfig("node1", map: true));
            node1.Setup(x => x.SelectBucketAsync("default", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException());
            var httpFetches = 0;
            fixture.SetupHttpConfig("node1", () =>
            {
                httpFetches++;
                return OneNodeBucketConfig("node1", map: true);
            });
            var node2 = fixture.SetupNewNode("node2", () => OneNodeBucketConfig("node2", map: true));

            var bucket = Assert.IsType<CouchbaseBucket>(await fixture.Context.GetOrCreateBucketAsync("default")
                .AsTask().WaitAsync(Deadline));

            Assert.Equal(0, httpFetches);
            node1.Verify(x => x.Dispose(), Times.AtLeastOnce);
            Assert.Same(node2.Object, Assert.Single(fixture.Context.Nodes));
            Assert.Same(bucket, node2.Object.Owner);
        }

        [Fact]
        public async Task GetOrCreateBucketAsync_HttpConfigAfterSelectFailureHasNoMap_WaitsForTheMapOverHttp()
        {
            var clock = new SteppableClock();
            using var fixture = new BucketOpenFixture(clock, "node1");
            var kvFetches = 0;
            var node = fixture.AddUnassignedNode("node1", () =>
            {
                kvFetches++;
                return OneNodeBucketConfig("node1", map: true);
            });
            node.Setup(x => x.SelectBucketAsync("default", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DocumentNotFoundException("Failed to select bucket default."));
            var configs = new Queue<BucketConfig>([
                OneNodeBucketConfig("node1", map: false),
                OneNodeBucketConfig("node1", map: true)
            ]);
            fixture.SetupHttpConfig("node1", configs.Dequeue);

            var open = fixture.Context.GetOrCreateBucketAsync("default").AsTask();
            await clock.WaitForSleepAsync(MapFetchInterval).WaitAsync(Deadline);
            Assert.False(open.IsCompleted);
            clock.Advance(MapFetchInterval);
            var bucket = Assert.IsType<CouchbaseBucket>(await open.WaitAsync(Deadline));

            Assert.Empty(configs);
            Assert.Equal(0, kvFetches);
            Assert.NotNull(bucket.KeyMapper);
            Assert.Same(bucket, node.Object.Owner);
        }

        /// <summary>
        /// The config of bucket "default" on one node. A Couchbase bucket has four vBuckets, or an empty map like a
        /// new bucket on a server before 7.6.2 (MB-60405). A Memcached bucket has no vBucket map.
        /// </summary>
        private static BucketConfig OneNodeBucketConfig(string host, bool map, bool memcached = false)
        {
            var config = new BucketConfig
            {
                Name = "default",
                Rev = 1,
                BucketCapabilities = memcached ? [] : ["cccp"],
                Nodes = [new Node { Hostname = $"{host}:8091", Ports = new Ports { Direct = 11210 } }],
                NodesExt = [new NodesExt { Hostname = host, Services = new Services { Kv = 11210 } }]
            };
            if (!memcached)
            {
                config.VBucketServerMap = new VBucketServerMapDto
                {
                    HashAlgorithm = "CRC",
                    ServerList = [$"{host}:11210"],
                    VBucketMap = map ? [[0], [0], [0], [0]] : []
                };
            }

            config.OnDeserialized();
            return config;
        }

        /// <summary>
        /// Checks that the opens created <paramref name="failed"/> buckets besides <paramref name="opened"/>, and that
        /// each of them stopped its bootstrapper and its config refresher. Only a failed bucket stops its refresher
        /// before the fixture is disposed.
        /// </summary>
        private static async Task AssertFailedBucketsDisposedAsync(BucketOpenFixture fixture, int failed,
            CouchbaseBucket opened = null)
        {
            var bootstrappers = fixture.Bootstrappers.Where(b => !ReferenceEquals(b, opened?.Bootstrapper)).ToList();
            Assert.Equal(failed, bootstrappers.Count);
            foreach (var bootstrapper in bootstrappers)
            {
                Mock.Get(bootstrapper).Verify(x => x.Dispose(), Times.Once);
            }

            await fixture.RefresherStops.WaitForAsync(failed).WaitAsync(Deadline);
        }

        /// <summary>
        /// A context that opens real buckets on mock nodes. Each bucket gets a mock bootstrapper.
        /// </summary>
        private sealed class BucketOpenFixture : IDisposable
        {
            private readonly Mock<IClusterNodeFactory> _nodeFactory = new(MockBehavior.Strict);
            private readonly Mock<HttpClusterMapBase> _httpClusterMap = new();

            public BucketOpenFixture(TimeProvider clock, params string[] hosts)
            {
                var bootstrapperFactory = new Mock<IBootstrapperFactory>();
                bootstrapperFactory.Setup(x => x.Create(It.IsAny<TimeSpan>()))
                    .Returns(() =>
                    {
                        var bootstrapper = new Mock<IBootstrapper>().Object;
                        Bootstrappers.Enqueue(bootstrapper);
                        return bootstrapper;
                    });
                var httpClusterMapFactory = new Mock<IHttpClusterMapFactory>();
                httpClusterMapFactory.Setup(x => x.Create(It.IsAny<ClusterContext>())).Returns(_httpClusterMap.Object);
                var refresherLogger = new Mock<ILogger<BucketConfigRefresher>>();
                refresherLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
                refresherLogger.Setup(x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((state, _) => state.ToString() == "The config refresher of default stopped"),
                        null, It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                    .Callback(RefresherStops.Increment);

                var options = new ClusterOptions()
                    .WithConnectionString($"couchbase://{string.Join(",", hosts)}?random_seed_nodes=false")
                    .WithPasswordAuthentication("username", "password");
                options.EnableDnsSrvResolution = false;
                options.ConfigPollFloorInterval = MapFetchInterval;
                options.KvTimeout = MapWaitTimeout;
                options.AddClusterService<TimeProvider>(clock);
                options.AddClusterService(_nodeFactory.Object);
                options.AddClusterService(ConfigHandler.Object);
                options.AddClusterService(bootstrapperFactory.Object);
                options.AddClusterService(httpClusterMapFactory.Object);
                options.AddClusterService(Logger.Object);
                options.AddClusterService(refresherLogger.Object);
                Context = new ClusterContext(new CancellationTokenSource(), options);
            }

            public ClusterContext Context { get; }

            public Mock<ILogger<ClusterContext>> Logger { get; } = new();

            /// <summary>
            /// Counts the bucket config refreshers whose loop stopped.
            /// </summary>
            public AsyncCounter RefresherStops { get; } = new();

            public Mock<IConfigHandler> ConfigHandler { get; } = new();

            /// <summary>
            /// The bootstrapper of each bucket that an open created, in order.
            /// </summary>
            public ConcurrentQueue<IBootstrapper> Bootstrappers { get; } = new();

            /// <summary>
            /// Adds a node with no owner, as the cluster bootstrap leaves it. Over HTTP streaming, KV does not serve
            /// the config.
            /// </summary>
            public Mock<IClusterNode> AddUnassignedNode(string host, Func<BucketConfig> fetch, bool httpStreaming = false)
            {
                var node = BucketNode(host, fetch, httpStreaming);
                Context.AddNode(node.Object);
                return node;
            }

            /// <summary>
            /// Sets up the node factory to create and connect a node.
            /// </summary>
            public Mock<IClusterNode> SetupNewNode(string host, Func<BucketConfig> fetch)
            {
                var node = BucketNode(host, fetch, httpStreaming: false);
                SetupSeedOrBucketNode(_nodeFactory, node.Object.EndPoint, (_, _, _) => Task.FromResult(node.Object));
                return node;
            }

            /// <summary>
            /// Sets up the node factory to fail to connect a node after <paramref name="connecting"/> runs.
            /// </summary>
            public void SetupFailedConnect(string host, Action connecting) =>
                SetupSeedOrBucketNode(_nodeFactory, new HostEndpointWithPort(host, 11210), (_, _, _) =>
                {
                    connecting();
                    return Task.FromException<IClusterNode>(new TimeoutException());
                });

            /// <summary>
            /// Sets up the HTTP streaming config of bucket "default" on a host.
            /// </summary>
            public void SetupHttpConfig(string host, Func<BucketConfig> fetch) =>
                _httpClusterMap.Setup(x => x.GetClusterMapAsync("default",
                        It.Is<HostEndpoint>(e => e.Host == host), It.IsAny<CancellationToken>()))
                    .Returns(() => Task.FromResult(fetch()));

            private Mock<IClusterNode> BucketNode(string host, Func<BucketConfig> fetch, bool httpStreaming)
            {
                var endPoint = new HostEndpointWithPort(host, 11210);
                var node = new Mock<IClusterNode>();
                node.SetupAllProperties();
                node.Setup(x => x.EndPoint).Returns(endPoint);
                node.Setup(x => x.KeyEndPoints).Returns([endPoint]);
                node.Setup(x => x.IsAssigned).Returns(() => node.Object.Owner is not null);
                node.Setup(x => x.BucketName).Returns(() => node.Object.Owner?.Name);
                if (httpStreaming)
                {
                    node.Setup(x => x.GetClusterMap(null, null, It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new DocumentNotFoundException());
                    SetupHttpConfig(host, fetch);
                }
                else
                {
                    node.Setup(x => x.GetClusterMap(null, null, It.IsAny<CancellationToken>()))
                        .Returns(() => Task.FromResult(fetch()));
                }

                return node;
            }

            public void Dispose() => Context.Dispose();
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

        private static IClusterNode NodeSupporting(string host, params ServerFeatures[] features) =>
            Mock.Of<IClusterNode>(node =>
                node.ServerFeatures == new ServerFeatureSet(features) &&
                node.EndPoint == new HostEndpointWithPort(host, 11210));

        #endregion

        #region GlobalConfig updates

        private const string GlobalHost1 = "10.112.192.101";
        private const string GlobalHost2 = "10.112.192.102";
        private const string GlobalHost3 = "10.112.192.103";

        [Fact]
        public async Task ConfigUpdatedAsync_NewerConfigWithSameNodes_ReplacesGlobalConfig()
        {
            using var context = CreateContextWithGlobalConfig(
                CreateGlobalConfig(10, vectorSearch: false, GlobalHost1, GlobalHost2));
            var incoming = CreateGlobalConfig(11, vectorSearch: true, GlobalHost1, GlobalHost2);

            await context.ConfigUpdatedAsync(incoming);

            Assert.Same(incoming, context.GlobalConfig);
            Assert.True(context.GlobalConfig.HasClusterCap(ClusterCapabilities.VECTOR_SEARCH));
        }

        [Fact]
        public async Task ConfigUpdatedAsync_NewerConfigWithRemovedNode_ReplacesGlobalConfig()
        {
            using var context = CreateContextWithGlobalConfig(
                CreateGlobalConfig(10, vectorSearch: false, GlobalHost1, GlobalHost2));
            var incoming = CreateGlobalConfig(11, vectorSearch: true, GlobalHost1);

            await context.ConfigUpdatedAsync(incoming);

            Assert.Contains(context.Nodes, node => node.EndPoint.Host == GlobalHost1);
            Assert.DoesNotContain(context.Nodes, node => node.EndPoint.Host == GlobalHost2);
            Assert.Same(incoming, context.GlobalConfig);
            Assert.True(context.GlobalConfig.HasClusterCap(ClusterCapabilities.VECTOR_SEARCH));
        }

        [Theory]
        [InlineData(3UL, 5UL, true)]
        [InlineData(1UL, 50UL, false)]
        public async Task ConfigUpdatedAsync_ComparesRevEpochBeforeRev(ulong revEpoch, ulong rev, bool replaces)
        {
            var current = CreateGlobalConfig(revEpoch: 2, rev: 10, vectorSearch: false, GlobalHost1, GlobalHost2);
            using var context = CreateContextWithGlobalConfig(current);
            var incoming = CreateGlobalConfig(revEpoch, rev, vectorSearch: false, GlobalHost1, GlobalHost2);

            await context.ConfigUpdatedAsync(incoming);

            Assert.Same(replaces ? incoming : current, context.GlobalConfig);
        }

        [Fact]
        public async Task ConfigUpdatedAsync_ConfigOlderThanLatest_IsIgnored()
        {
            using var context = CreateContextWithGlobalConfig(
                CreateGlobalConfig(10, vectorSearch: false, GlobalHost1, GlobalHost2));
            var latest = CreateGlobalConfig(12, vectorSearch: true, GlobalHost1, GlobalHost2);

            await context.ConfigUpdatedAsync(latest);
            await context.ConfigUpdatedAsync(CreateGlobalConfig(11, vectorSearch: false, GlobalHost1, GlobalHost2));

            Assert.Same(latest, context.GlobalConfig);
            Assert.True(context.GlobalConfig.HasClusterCap(ClusterCapabilities.VECTOR_SEARCH));
        }

        [Fact]
        public async Task ConfigUpdatedAsync_IncomingConfigWithoutClusterName_KeepsCurrentName()
        {
            var current = CreateGlobalConfig(10, vectorSearch: false, GlobalHost1, GlobalHost2);
            current.ClusterName = "cluster-a";
            using var context = CreateContextWithGlobalConfig(current);
            var incoming = CreateGlobalConfig(11, vectorSearch: false, GlobalHost1, GlobalHost2);

            await context.ConfigUpdatedAsync(incoming);

            Assert.Same(incoming, context.GlobalConfig);
            Assert.Equal("cluster-a", context.GlobalConfig.ClusterName);
        }

        [Theory]
        [InlineData(NetworkResolution.Auto)]
        [InlineData(NetworkResolution.External)]
        public async Task ConfigUpdatedAsync_FirstNodeWithoutAlternateAddress_KeepsExternalNetwork(string networkResolution)
        {
            var current = CreateGlobalConfig(10, vectorSearch: false, GlobalHost1, GlobalHost2);
            AddExternalAddress(current, GlobalHost1);
            using var context = CreateContextWithGlobalConfig(current, networkResolution);
            Assert.Equal(NetworkResolution.External, context.ClusterOptions.EffectiveNetworkResolution);

            var incoming = CreateGlobalConfig(11, vectorSearch: false, GlobalHost1, GlobalHost2);
            AddExternalAddress(incoming, GlobalHost1);
            incoming.NodesExt.Reverse();

            await context.ConfigUpdatedAsync(incoming);

            Assert.Same(incoming, context.GlobalConfig);
            Assert.Equal(NetworkResolution.External, context.ClusterOptions.EffectiveNetworkResolution);
        }

        [Fact]
        public async Task ConfigUpdatedAsync_NewNodeFailsToConnect_KeepsOldConfigUntilRetrySucceeds()
        {
            var nodeFactory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
            var current = CreateGlobalConfig(10, vectorSearch: false, GlobalHost1);
            using var context = CreateContextWithGlobalConfig(current, nodeFactory: nodeFactory.Object);

            // Nodes connect in NodesExt order, so node A is added before node B fails.
            var incoming = CreateGlobalConfig(11, vectorSearch: false, GlobalHost1, GlobalHost2);
            // Node B has its own host, so it never matches an existing node.
            incoming.NodesExt.Add(new NodesExt { Hostname = GlobalHost3, Services = new Services { Kv = 11210 } });

            var endPointA = new HostEndpointWithPort(GlobalHost2, 11210);
            var endPointB = new HostEndpointWithPort(GlobalHost3, 11210);
            nodeFactory.Setup(f => f.CreateAndConnectAsync(endPointA, It.IsAny<NodeAdapter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateGlobalNode(GlobalHost2));
            nodeFactory.Setup(f => f.CreateAndConnectAsync(endPointB, It.IsAny<NodeAdapter>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SocketException((int)SocketError.ConnectionRefused));

            await Assert.ThrowsAsync<SocketException>(() => context.ConfigUpdatedAsync(incoming));

            Assert.Same(current, context.GlobalConfig);
            Assert.Contains(context.Nodes, node => node.EndPoint.Host == GlobalHost2);

            nodeFactory.Setup(f => f.CreateAndConnectAsync(endPointB, It.IsAny<NodeAdapter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateGlobalNode(GlobalHost3));

            await context.ConfigUpdatedAsync(incoming);

            Assert.Same(incoming, context.GlobalConfig);
            Assert.Equal(new[] { GlobalHost1, GlobalHost2, GlobalHost3 },
                context.Nodes.Select(node => node.EndPoint.Host).OrderBy(host => host));
            nodeFactory.Verify(f => f.CreateAndConnectAsync(endPointA, It.IsAny<NodeAdapter>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private static void AddExternalAddress(BucketConfig config, string host)
        {
            // Same host as the internal address, so the existing node is still found by host name.
            config.NodesExt.Single(nodeExt => nodeExt.Hostname == host).AlternateAddresses =
                new Dictionary<string, ExternalAddressesConfig>
                {
                    [NetworkResolution.External] = new() { Hostname = host, Ports = new Services { Kv = 11210 } }
                };
        }

        private static BucketConfig CreateGlobalConfig(ulong rev, bool vectorSearch, params string[] hosts) =>
            CreateGlobalConfig(revEpoch: 0, rev, vectorSearch, hosts);

        private static BucketConfig CreateGlobalConfig(ulong revEpoch, ulong rev, bool vectorSearch, params string[] hosts)
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\cluster-level-config-rev69.json",
                InternalSerializationContext.Default.BucketConfig);

            config.RevEpoch = revEpoch;
            config.Rev = rev;
            config.Name = BucketConfig.GlobalBucketName;
            // Real global configs omit the vBucket map. The fixture sets it to null, which breaks SetEffectiveNetworkResolution.
            config.VBucketServerMap = new VBucketServerMapDto();
            config.NodesExt = config.NodesExt.Where(nodeExt => hosts.Contains(nodeExt.Hostname)).ToList();

            var caps = new Dictionary<string, IEnumerable<string>>
            {
                ["n1ql"] = new[] { "enhancedPreparedStatements" }
            };
            if (vectorSearch)
            {
                caps["search"] = new[] { "vectorSearch", "scopedSearchIndex" };
            }
            config.ClusterCapabilities = caps;
            config.OnDeserialized();

            return config;
        }

        private static ClusterContext CreateContextWithGlobalConfig(BucketConfig globalConfig,
            string networkResolution = NetworkResolution.Auto, IClusterNodeFactory nodeFactory = null)
        {
            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            options.NetworkResolution = networkResolution;
            // Strict by default, so a test that reaches the new node path fails instead of trying to connect.
            options.AddClusterService<IClusterNodeFactory>(nodeFactory ?? new Mock<IClusterNodeFactory>(MockBehavior.Strict).Object);
            var context = new ClusterContext(new CancellationTokenSource(), options);

            globalConfig.SetEffectiveNetworkResolution(options);
            context.GlobalConfig = globalConfig;
            context.UpdateClusterCapsConfig(globalConfig);
            // A node gets its adapter at bootstrap. The prune skips a node with no adapter, as a bootstrap still uses it.
            foreach (var nodeAdapter in globalConfig.GetNodes())
            {
                var node = CreateGlobalNode(nodeAdapter.Hostname);
                node.NodesAdapter = nodeAdapter;
                context.AddNode(node);
            }

            return context;
        }

        private static IClusterNode CreateGlobalNode(string host)
        {
            var endPoint = new HostEndpointWithPort(host, 11210);
            var node = new Mock<IClusterNode>();
            node.Setup(n => n.EndPoint).Returns(endPoint);
            node.Setup(n => n.KeyEndPoints).Returns(new[] { endPoint });
            node.Setup(n => n.Owner).Returns((IBucket)null);
            node.SetupProperty(n => n.NodesAdapter);
            return node.Object;
        }

        #endregion

        #region Cluster capabilities

        [Fact]
        public async Task ConfigUpdatedAsync_NewerBucketConfigWithNewCap_HasClusterCap()
        {
            using var context = CreateContextWithGlobalConfig(
                CreateGlobalConfig(10, vectorSearch: false, GlobalHost1, GlobalHost2));

            await context.ConfigUpdatedAsync(CreateBucketConfig(0, 11, "vectorSearch"));

            Assert.True(context.HasClusterCap(ClusterCapabilities.VECTOR_SEARCH));
            Assert.False(context.GlobalConfig.HasClusterCap(ClusterCapabilities.VECTOR_SEARCH));
        }

        [Fact]
        public async Task ConfigUpdatedAsync_OlderBucketConfigWithFewerCaps_KeepsCap()
        {
            using var context = CreateContextWithGlobalConfig(
                CreateGlobalConfig(10, vectorSearch: true, GlobalHost1, GlobalHost2));

            await context.ConfigUpdatedAsync(CreateBucketConfig(0, 9));

            Assert.True(context.HasClusterCap(ClusterCapabilities.VECTOR_SEARCH));
            context.AssertClusterCap(ClusterCapabilities.VECTOR_SEARCH);
        }

        [Fact]
        public async Task ConfigUpdatedAsync_HigherRevEpoch_WinsOverHigherRev()
        {
            using var context = CreateContextWithGlobalConfig(
                CreateGlobalConfig(10, vectorSearch: false, GlobalHost1, GlobalHost2));

            await context.ConfigUpdatedAsync(CreateBucketConfig(1, 5, "vectorSearch"));
            await context.ConfigUpdatedAsync(CreateBucketConfig(0, 100));

            Assert.True(context.HasClusterCap(ClusterCapabilities.VECTOR_SEARCH));
            Assert.Equal(new ConfigVersion(1, 5), context.ClusterCapsConfig.ConfigVersion);
        }

        [Fact]
        public async Task ConfigUpdatedAsync_NewerConfigWithoutCaps_KeepsCaps()
        {
            var current = CreateGlobalConfig(10, vectorSearch: true, GlobalHost1, GlobalHost2);
            using var context = CreateContextWithGlobalConfig(current);
            var incoming = CreateBucketConfig(0, 11);
            incoming.ClusterCapabilities = null;

            await context.ConfigUpdatedAsync(incoming);

            Assert.Same(current, context.ClusterCapsConfig);
            Assert.True(context.HasClusterCap(ClusterCapabilities.VECTOR_SEARCH));
        }

        [Fact]
        public async Task UpdateClusterCapsConfig_Replace_StoresOlderVersion()
        {
            using var context = CreateContextWithGlobalConfig(
                CreateGlobalConfig(10, vectorSearch: true, GlobalHost1, GlobalHost2));
            await context.ConfigUpdatedAsync(CreateBucketConfig(0, 20, "vectorSearch"));
            var notifications = 0;
            context.ClusterCapsChanged += () => notifications++;

            var bootstrap = CreateGlobalConfig(5, vectorSearch: false, GlobalHost1, GlobalHost2);
            context.UpdateClusterCapsConfig(bootstrap, replace: true);

            Assert.Same(bootstrap, context.ClusterCapsConfig);
            Assert.False(context.HasClusterCap(ClusterCapabilities.VECTOR_SEARCH));
            Assert.Equal(1, notifications);
        }

        [Fact]
        public void AssertClusterCap_NoCapsKnown_DoesNotThrow()
        {
            using var context = new ClusterContext(new CancellationTokenSource(),
                new ClusterOptions().WithPasswordAuthentication("username", "password"));

            context.AssertClusterCap(ClusterCapabilities.VECTOR_SEARCH);

            Assert.Null(context.HasClusterCap(ClusterCapabilities.VECTOR_SEARCH));
        }

        [Fact]
        public async Task ConfigUpdatedAsync_CapsChange_NotifiesOnce()
        {
            using var context = CreateContextWithGlobalConfig(
                CreateGlobalConfig(10, vectorSearch: false, GlobalHost1, GlobalHost2));
            var notifications = 0;
            context.ClusterCapsChanged += () => notifications++;

            // An empty search section flattens to the same set as no search section.
            await context.ConfigUpdatedAsync(CreateBucketConfig(0, 11));
            Assert.Equal(0, notifications);

            await context.ConfigUpdatedAsync(CreateBucketConfig(0, 12, "vectorSearch"));
            Assert.Equal(1, notifications);

            await context.ConfigUpdatedAsync(CreateBucketConfig(0, 13, "vectorSearch"));
            await context.ConfigUpdatedAsync(CreateBucketConfig(0, 12, "scoreFusion"));
            Assert.Equal(1, notifications);
        }

        [Fact]
        public async Task UpdateClusterCapsConfig_ConcurrentReadersAndWriters_EndsAtNewestCaps()
        {
            using var context = CreateContextWithGlobalConfig(
                CreateGlobalConfig(10, vectorSearch: false, GlobalHost1, GlobalHost2));
            const ulong lastRev = 20_000;
            // Two writers apply interleaved rising versions. Only the newest config has scoreFusion.
            var configs = Enumerable.Range(11, (int)lastRev - 10)
                .Select(rev => CreateBucketConfig(0, (ulong)rev,
                    rev == (int)lastRev ? "scoreFusion" : rev % 2 == 0 ? "vectorSearch" : "scopedSearchIndex"))
                .ToArray();
            var done = 0;

            var readers = Enumerable.Range(0, 4).Select(_ => Task.Factory.StartNew(() =>
            {
                var last = new ConfigVersion(0, 0);
                do
                {
                    context.HasClusterCap(ClusterCapabilities.VECTOR_SEARCH);
                    var version = context.ClusterCapsConfig.ConfigVersion;
                    Assert.True(version >= last, $"Version went back from {last} to {version}.");
                    last = version;
                } while (Volatile.Read(ref done) == 0);
            }, TaskCreationOptions.LongRunning)).ToArray();

            var writers = Enumerable.Range(0, 2).Select(writer => Task.Factory.StartNew(() =>
            {
                for (var i = writer; i < configs.Length; i += 2)
                {
                    context.UpdateClusterCapsConfig(configs[i]);
                }
            }, TaskCreationOptions.LongRunning)).ToArray();

            await Task.WhenAll(writers);
            Volatile.Write(ref done, 1);
            await Task.WhenAll(readers);

            Assert.Equal(new ConfigVersion(0, lastRev), context.ClusterCapsConfig.ConfigVersion);
            Assert.True(context.HasClusterCap(ClusterCapabilities.SCORE_FUSION));
            Assert.False(context.HasClusterCap(ClusterCapabilities.VECTOR_SEARCH));
        }

        private static BucketConfig CreateBucketConfig(ulong revEpoch, ulong rev, params string[] searchCaps)
        {
            var config = new BucketConfig
            {
                Name = "default",
                RevEpoch = revEpoch,
                Rev = rev,
                ClusterCapabilities = new Dictionary<string, IEnumerable<string>>
                {
                    ["n1ql"] = new[] { "enhancedPreparedStatements" },
                    ["search"] = searchCaps
                }
            };
            config.OnDeserialized();
            return config;
        }

        #endregion

        #region Nodes that share a host

        // On a local Docker cluster every node has the external host localhost with its own ports.
        // Nodes with no K/V service then share the endpoint localhost:0.

        [Fact]
        public async Task BootstrapGlobal_Shared_Host_Creates_A_Node_Per_Map_Node()
        {
            var config = DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, query: true), DockerNode(3, search: true));
            var seed = SeedNode(new HostEndpointWithPort("localhost", DockerKvPort(0)), config);
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created, seed.Object)));

            await AssertCompleted(context.BootstrapGlobalAsync());

            AssertNodePerMapNode(context, config);
            Assert.Contains(context.Nodes, node => ReferenceEquals(node, seed.Object));
            Assert.Equal(DockerHost(0), seed.Object.NodesAdapter.CanonicalHostname);
            Assert.Equal(3, created.Count);
        }

        // On Capella Private Link every node has the external host of the endpoint. The seed port 11207
        // leads to any node, and each node has its own K/V port from 11208.
        [Fact]
        public async Task BootstrapGlobal_Seed_That_Matches_No_Map_Node_Is_Removed()
        {
            const string privateEndpoint = "private-endpoint.ui0cn1dkh45zmrb.cloud.couchbase.com";
            var config = ResourceHelper.ReadResource(@"Documents\Configs\private-link.json",
                InternalSerializationContext.Default.BucketConfig);
            var seed = SeedNode(new HostEndpointWithPort(privateEndpoint, 11207), config);
            var created = new List<IClusterNode>();
            var options = new ClusterOptions().WithConnectionString($"couchbases://{privateEndpoint}")
                .WithPasswordAuthentication("username", "password");
            options.EnableDnsSrvResolution = false;
            options.AddClusterService(NodeFactory(created, seed.Object));
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(), options);

            await AssertCompleted(context.BootstrapGlobalAsync());

            AssertNodePerMapNode(context, config);
            Assert.DoesNotContain(context.Nodes, node => ReferenceEquals(node, seed.Object));
            seed.Verify(node => node.Dispose(), Times.Once);
            Assert.Equal(3, created.Count);
        }

        [Fact]
        public async Task BootstrapGlobal_Node_Connect_Fails_Removes_Unmatched_Seed()
        {
            const string privateEndpoint = "private-endpoint.ui0cn1dkh45zmrb.cloud.couchbase.com";
            var config = ResourceHelper.ReadResource(@"Documents\Configs\private-link.json",
                InternalSerializationContext.Default.BucketConfig);
            var seed = SeedNode(new HostEndpointWithPort(privateEndpoint, 11207), config);
            var options = new ClusterOptions().WithConnectionString($"couchbases://{privateEndpoint}")
                .WithPasswordAuthentication("username", "password");
            options.EnableDnsSrvResolution = false;
            options.AddClusterService(NodeFactory([], seed.Object, failingHost: config.NodesExt[1].Hostname));
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(), options);

            await AssertCompleted(context.BootstrapGlobalAsync());

            Assert.DoesNotContain(context.Nodes, node => ReferenceEquals(node, seed.Object));
            seed.Verify(node => node.Dispose(), Times.Once);
        }

        // The seed is the second map node, so the first map node fails before the loop reaches the seed.
        [Fact]
        public async Task BootstrapGlobal_Node_Connect_Fails_Keeps_Seed_Of_A_Later_Map_Node()
        {
            var config = DockerConfig(1, DockerNode(1, kv: true), DockerNode(0, kv: true));
            var seed = SeedNode(new HostEndpointWithPort("localhost", DockerKvPort(0)), config);
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory([], seed.Object, failingHost: DockerHost(1))));

            await AssertCompleted(context.BootstrapGlobalAsync());

            Assert.Contains(context.Nodes, node => ReferenceEquals(node, seed.Object));
            seed.Verify(node => node.Dispose(), Times.Never);
        }

        [Fact]
        public async Task ConfigUpdated_Shared_Host_Creates_A_Node_For_A_New_Map_Node()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created)));
            var nodes = AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, query: true), DockerNode(3, search: true)));
            var hosts = nodes.Select(node => node.NodesAdapter.CanonicalHostname).ToList();
            var newer = DockerConfig(2, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, query: true), DockerNode(3, search: true), DockerNode(4, search: true));

            await AssertCompleted(context.ConfigUpdatedAsync(newer));

            AssertNodePerMapNode(context, newer);
            Assert.Single(created);
            Assert.All(nodes, node => Assert.Contains(context.Nodes, x => ReferenceEquals(x, node)));
            Assert.Equal(hosts, nodes.Select(node => node.NodesAdapter.CanonicalHostname));
        }

        [Fact]
        public async Task ConfigUpdated_Shared_Host_Removes_Only_The_Node_That_Left()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created)));
            var nodes = AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, query: true), DockerNode(3, search: true)));
            var leaving = nodes.Single(node => node.HasQuery);
            var staying = nodes.Where(node => node != leaving).ToList();
            var hosts = staying.Select(node => node.NodesAdapter.CanonicalHostname).ToList();
            var newer = DockerConfig(2, DockerNode(0, kv: true), DockerNode(1, kv: true), DockerNode(3, search: true));

            await AssertCompleted(context.ConfigUpdatedAsync(newer));

            AssertNodePerMapNode(context, newer);
            Assert.Empty(created);
            Assert.DoesNotContain(context.Nodes, node => ReferenceEquals(node, leaving));
            Assert.All(staying, node => Assert.Contains(context.Nodes, x => ReferenceEquals(x, node)));
            Assert.Equal(hosts, staying.Select(node => node.NodesAdapter.CanonicalHostname));
        }

        // A bucket takes over the global K/V nodes, which changes their bucket name (NCBC-4153).
        [Fact]
        public async Task ConfigUpdated_Shared_Host_Finds_Nodes_Owned_By_A_Bucket()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created)));
            var nodes = AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, query: true), DockerNode(3, search: true)));
            var bucket = Mock.Of<IBucket>(b => b.Name == "default");
            foreach (var node in nodes.Where(node => node.HasKv))
            {
                node.Owner = bucket;
            }

            var hosts = nodes.Select(node => node.NodesAdapter.CanonicalHostname).ToList();
            var newer = DockerConfig(2, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, query: true), DockerNode(3, search: true));

            await AssertCompleted(context.ConfigUpdatedAsync(newer));

            AssertNodePerMapNode(context, newer);
            Assert.Empty(created);
            Assert.Equal(hosts, nodes.Select(node => node.NodesAdapter.CanonicalHostname));
        }

        // Nodes with no K/V service never get a bucket owner, so every bucket map looks them up as unassigned nodes.
        [Fact]
        public async Task ProcessClusterMap_Shared_Host_Nodes_Keep_Their_Own_Adapters()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created)));
            var nodes = AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, query: true), DockerNode(3, search: true)));
            var hosts = nodes.Select(node => node.NodesAdapter.CanonicalHostname).ToList();
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());

            for (ulong rev = 1; rev <= 2; rev++)
            {
                var bucketConfig = DockerConfig(rev, DockerNode(0, kv: true), DockerNode(1, kv: true),
                    DockerNode(2, query: true), DockerNode(3, search: true));
                bucketConfig.Name = "default";
                await AssertCompleted(context.ProcessClusterMapAsync(bucket, bucketConfig));
            }

            Assert.Empty(created);
            Assert.Equal(4, context.Nodes.Count);
            Assert.Equal(hosts, nodes.Select(node => node.NodesAdapter.CanonicalHostname));
        }

        // On cluster_run every node has the host 127.0.0.1 and no alternate address.
        // Only the management port tells apart the nodes with no K/V service.
        [Fact]
        public async Task ProcessClusterMap_Shared_Default_Host_Nodes_Keep_Their_Own_Adapters()
        {
            var created = new List<IClusterNode>();
            var options = new ClusterOptions().WithConnectionString("couchbase://127.0.0.1:12000")
                .WithPasswordAuthentication("username", "password");
            options.AddClusterService(NodeFactory(created));
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(), options);
            var nodes = AddGlobalNodes(context, ClusterRunConfig(1, BucketConfig.GlobalBucketName));
            Assert.Equal(new[] { 12000, 12002, 0, 0 }, nodes.Select(node => node.EndPoint.Port));
            var query = nodes[2];
            var search = nodes[3];
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());

            for (ulong rev = 1; rev <= 2; rev++)
            {
                await AssertCompleted(context.ProcessClusterMapAsync(bucket, ClusterRunConfig(rev, "default")));
            }

            Assert.Empty(created);
            Assert.Equal(4, context.Nodes.Count);
            Assert.Equal((9002, true, false), (query.NodesAdapter.MgmtApi, query.HasQuery, query.HasSearch));
            Assert.Equal((9003, false, true), (search.NodesAdapter.MgmtApi, search.HasQuery, search.HasSearch));
            var kept = DiagnosticsReportProvider.NewestPerNode(context.Nodes).Select(host => host.Node).ToList();
            Assert.Contains(kept, node => ReferenceEquals(node, query));
            Assert.Contains(kept, node => ReferenceEquals(node, search));
        }

        [Fact]
        public async Task ProcessClusterMap_New_Node_Select_Bucket_Fails_Disposes_The_Node()
        {
            var failedKvPort = 12002;
            var failingPool = FailingSelectPool();
            var created = new List<IClusterNode>();
            var factory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
            factory.Setup(f => f.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<NodeAdapter>(),
                    It.IsAny<CancellationToken>()))
                .Returns((HostEndpointWithPort endPoint, NodeAdapter nodeAdapter, CancellationToken _) =>
                {
                    var pool = endPoint.Port == failedKvPort && created.Count(n => n.EndPoint.Equals(endPoint)) == 0
                        ? failingPool.Object
                        : null;
                    var node = CreateGlobalNode(endPoint, nodeAdapter, pool);
                    created.Add(node);
                    return Task.FromResult(node);
                });
            var options = new ClusterOptions().WithConnectionString("couchbase://127.0.0.1:12000")
                .WithPasswordAuthentication("username", "password");
            options.AddClusterService(factory.Object);
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(), options);
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());

            await Assert.ThrowsAsync<SocketException>(() =>
                AssertCompleted(context.ProcessClusterMapAsync(bucket, ClusterRunBucketConfig(1))));

            var failed = created.Single(node => node.EndPoint.Port == failedKvPort);
            Assert.DoesNotContain(context.Nodes, node => ReferenceEquals(node, failed));
            failingPool.Verify(pool => pool.Dispose(), Times.Once);

            await AssertCompleted(context.ProcessClusterMapAsync(bucket, ClusterRunBucketConfig(1)));

            Assert.Equal(4, context.Nodes.Count);
            var fresh = context.Nodes.Single(node => node.EndPoint.Port == failedKvPort);
            Assert.NotSame(failed, fresh);
            Assert.Same(bucket, fresh.Owner);
        }

        [Fact]
        public async Task ProcessClusterMap_Unassigned_Node_Select_Bucket_Fails_Removes_And_Disposes_The_Node()
        {
            var failedKvPort = 12002;
            var failingPool = FailingSelectPool();
            var created = new List<IClusterNode>();
            var options = new ClusterOptions().WithConnectionString("couchbase://127.0.0.1:12000")
                .WithPasswordAuthentication("username", "password");
            options.AddClusterService(NodeFactory(created));
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(), options);
            var nodes = AddGlobalNodes(context, ClusterRunConfig(1, BucketConfig.GlobalBucketName),
                endPoint => endPoint.Port == failedKvPort ? failingPool.Object : null);
            var failed = nodes.Single(node => node.EndPoint.Port == failedKvPort);
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());

            await Assert.ThrowsAsync<SocketException>(() =>
                AssertCompleted(context.ProcessClusterMapAsync(bucket, ClusterRunBucketConfig(1))));

            Assert.DoesNotContain(context.Nodes, node => ReferenceEquals(node, failed));
            failingPool.Verify(pool => pool.Dispose(), Times.Once);

            await AssertCompleted(context.ProcessClusterMapAsync(bucket, ClusterRunBucketConfig(1)));

            Assert.Equal(4, context.Nodes.Count);
            var fresh = Assert.Single(created);
            Assert.Equal(failedKvPort, fresh.EndPoint.Port);
            Assert.Same(bucket, fresh.Owner);
            Assert.Contains(context.Nodes, node => ReferenceEquals(node, fresh));
        }

        private static Mock<IConnectionPool> FailingSelectPool()
        {
            var pool = new Mock<IConnectionPool>();
            pool.Setup(p => p.SelectBucketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SocketException((int)SocketError.ConnectionReset));
            return pool;
        }

        private static BucketConfig ClusterRunBucketConfig(ulong rev)
        {
            var config = ClusterRunConfig(rev, "default");
            config.VBucketServerMap = new VBucketServerMapDto
            {
                HashAlgorithm = "CRC",
                NumReplicas = 1,
                ServerList = ["127.0.0.1:12000", "127.0.0.1:12002"],
                VBucketMap = [[0, 1], [1, 0]]
            };
            return config;
        }

        private static string DockerHost(int index) => $"172.18.0.{index + 2}";

        private static int DockerKvPort(int index) => 32010 + 100 * index;

        private static NodesExt DockerNode(int index, bool kv = false, bool query = false, bool search = false)
        {
            var portBase = 32000 + 100 * index;
            return new NodesExt
            {
                Hostname = DockerHost(index),
                Services = new Services
                {
                    Mgmt = 8091, Kv = kv ? 11210 : 0, N1Ql = query ? 8093 : 0, Fts = search ? 8094 : 0
                },
                AlternateAddresses = new Dictionary<string, ExternalAddressesConfig>
                {
                    [NetworkResolution.External] = new()
                    {
                        Hostname = "localhost",
                        Ports = new Services
                        {
                            Mgmt = portBase + 91,
                            Kv = kv ? DockerKvPort(index) : 0,
                            N1Ql = query ? portBase + 93 : 0,
                            Fts = search ? portBase + 94 : 0
                        }
                    }
                }
            };
        }

        private static BucketConfig DockerConfig(ulong rev, params NodesExt[] nodes)
        {
            var config = new BucketConfig { Rev = rev, NodesExt = nodes.ToList() };
            config.OnDeserialized();
            return config;
        }

        /// <summary>
        /// Two K/V nodes, a query node and a search node, all on 127.0.0.1 with their own ports.
        /// </summary>
        private static BucketConfig ClusterRunConfig(ulong rev, string name)
        {
            NodesExt[] nodes =
            [
                ClusterRunNode(0, kv: true), ClusterRunNode(1, kv: true),
                ClusterRunNode(2, query: true), ClusterRunNode(3, search: true)
            ];
            var config = DockerConfig(rev, nodes);
            config.Name = name;

            // With no alternate address, a bucket map node keeps its K/V port only if nodes lists it.
            config.Nodes = nodes.Where(node => node.Services.Kv > 0)
                .Select(node => new Node { Hostname = $"{node.Hostname}:{node.Services.Mgmt}" })
                .ToList();
            return config;
        }

        private static NodesExt ClusterRunNode(int index, bool kv = false, bool query = false, bool search = false) =>
            new()
            {
                Hostname = "127.0.0.1",
                Services = new Services
                {
                    Mgmt = 9000 + index,
                    Kv = kv ? 12000 + 2 * index : 0,
                    N1Ql = query ? 9499 - index : 0,
                    Fts = search ? 9200 + index : 0
                }
            };

        private static ClusterOptions DockerOptions(IClusterNodeFactory nodeFactory)
        {
            var options = new ClusterOptions().WithConnectionString($"couchbase://localhost:{DockerKvPort(0)}")
                .WithPasswordAuthentication("username", "password");
            options.NetworkResolution = NetworkResolution.External;
            options.AddClusterService(nodeFactory);
            return options;
        }

        private static Mock<IClusterNode> SeedNode(HostEndpointWithPort endPoint, BucketConfig config)
        {
            var seed = new Mock<IClusterNode>();
            seed.SetupAllProperties();
            seed.Setup(node => node.EndPoint).Returns(endPoint);
            seed.Setup(node => node.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(config);
            return seed;
        }

        private static IClusterNodeFactory NodeFactory(List<IClusterNode> created, IClusterNode seed = null,
            string failingHost = null, string gatedHost = null, Task connectGate = null)
        {
            var factory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
            if (seed != null)
            {
                factory.Setup(f => f.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(seed);
            }

            Task<IClusterNode> Create(HostEndpointWithPort endPoint, NodeAdapter nodeAdapter, List<IClusterNode> list)
            {
                if (failingHost != null && nodeAdapter.CanonicalHostname == failingHost)
                {
                    return Task.FromException<IClusterNode>(new SocketException((int)SocketError.ConnectionRefused));
                }

                if (gatedHost != null && nodeAdapter.CanonicalHostname == gatedHost)
                {
                    return CreateAfterAsync(connectGate, endPoint, nodeAdapter, list);
                }

                var node = CreateGlobalNode(endPoint, nodeAdapter);
                list.Add(node);
                return Task.FromResult(node);
            }

            factory.Setup(f => f.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<NodeAdapter>(),
                    It.IsAny<CancellationToken>()))
                .Returns((HostEndpointWithPort endPoint, NodeAdapter nodeAdapter, CancellationToken _) =>
                    Create(endPoint, nodeAdapter, created));

            // Bucket nodes do not go in created, which counts the nodes with no bucket.
            var bucketNodes = new List<IClusterNode>();
            factory.Setup(f => f.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<NodeAdapter>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((HostEndpointWithPort endPoint, NodeAdapter nodeAdapter, string _, CancellationToken _) =>
                    Create(endPoint, nodeAdapter, bucketNodes));
            factory.Setup(f => f.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<NodeAdapter>(),
                    It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns((HostEndpointWithPort endPoint, NodeAdapter nodeAdapter, string _, bool _, CancellationToken _) =>
                    Create(endPoint, nodeAdapter, bucketNodes));
            return factory.Object;
        }

        /// <summary>
        /// Sets up every node factory call that connects a seed or a bucket node at <paramref name="endPoint"/>, or at
        /// any endpoint when it is null. The adapter is null for a seed.
        /// </summary>
        private static void SetupSeedOrBucketNode(Mock<IClusterNodeFactory> factory, HostEndpointWithPort? endPoint,
            Func<HostEndpointWithPort, NodeAdapter, CancellationToken, Task<IClusterNode>> create)
        {
            factory.Setup(f => f.CreateAndConnectAsync(It.Is<HostEndpointWithPort>(e => endPoint == null || e == endPoint),
                    It.IsAny<CancellationToken>()))
                .Returns((HostEndpointWithPort e, CancellationToken token) => create(e, null, token));
            factory.Setup(f => f.CreateAndConnectAsync(It.Is<HostEndpointWithPort>(e => endPoint == null || e == endPoint),
                    It.IsAny<NodeAdapter>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((HostEndpointWithPort e, NodeAdapter nodeAdapter, string _, CancellationToken token) =>
                    create(e, nodeAdapter, token));
            factory.Setup(f => f.CreateAndConnectAsync(It.Is<HostEndpointWithPort>(e => endPoint == null || e == endPoint),
                    It.IsAny<NodeAdapter>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns((HostEndpointWithPort e, NodeAdapter nodeAdapter, string _, bool _, CancellationToken token) =>
                    create(e, nodeAdapter, token));
        }

        private static async Task<IClusterNode> CreateAfterAsync(Task gate, HostEndpointWithPort endPoint,
            NodeAdapter nodeAdapter, List<IClusterNode> created)
        {
            await gate.ConfigureAwait(false);
            var node = CreateGlobalNode(endPoint, nodeAdapter);
            created.Add(node);
            return node;
        }

        private static List<IClusterNode> AddGlobalNodes(ClusterContext context, BucketConfig config,
            Func<HostEndpointWithPort, IConnectionPool> connectionPools = null)
        {
            context.GlobalConfig = config;
            var nodes = config.GetNodes()
                .Select(nodeAdapter =>
                {
                    var endPoint = HostEndpointWithPort.Create(nodeAdapter, context.ClusterOptions);
                    return CreateGlobalNode(endPoint, nodeAdapter, connectionPools?.Invoke(endPoint));
                })
                .ToList();
            nodes.ForEach(context.AddNode);
            return nodes;
        }

        /// <summary>
        /// Asserts one node per map node, at the endpoint of that map node and with its adapter.
        /// </summary>
        private static void AssertNodePerMapNode(ClusterContext context, BucketConfig config)
        {
            var expected = config.GetNodes()
                .Select(nodeAdapter => (Host: nodeAdapter.CanonicalHostname,
                    EndPoint: HostEndpointWithPort.Create(nodeAdapter, context.ClusterOptions)))
                .OrderBy(node => node.Host, StringComparer.Ordinal)
                .ToList();
            var actual = context.Nodes
                .Select(node => (Host: node.NodesAdapter?.CanonicalHostname, node.EndPoint))
                .OrderBy(node => node.Host, StringComparer.Ordinal)
                .ToList();
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Every dependency is a mock that completes at once, so the call must be complete when it returns.
        /// This bounds the await with no clock.
        /// </summary>
        private static Task AssertCompleted(Task task)
        {
            Assert.True(task.IsCompleted, "The call waits on something that no mock completes.");
            return task;
        }

        #endregion

        #region One cluster reconcile (NCBC-4423)

        // The cluster config apply, the bucket config apply and a reconcile that a newer view overtakes all end at the
        // node list of the newest config.
        [Theory]
        [InlineData("cluster")]
        [InlineData("bucket")]
        [InlineData("overtaken")]
        public async Task Reconcile_EveryCallSite_GivesTheNodesOfTheView(string callSite)
        {
            var created = new List<IClusterNode>();
            var connectGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created, gatedHost: PlainHost(4), connectGate: connectGate.Task)));
            var initial = DockerConfig(1, PlainNode(0, kv: true), PlainNode(1, kv: true), PlainNode(2, query: true));
            AddGlobalNodes(context, initial);
            context.UpdateClusterNodesConfig(initial);
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());

            switch (callSite)
            {
                case "cluster":
                    await AssertCompleted(context.ConfigUpdatedAsync(DockerConfig(2, PlainNode(0, kv: true),
                        PlainNode(1, kv: true), PlainNode(3, query: true))));
                    break;
                case "bucket":
                    await AssertCompleted(context.ProcessClusterMapAsync(bucket, PlainBucketConfig(2, "default",
                        PlainNode(0, kv: true), PlainNode(1, kv: true), PlainNode(3, query: true))));
                    break;
                default:
                    // The cluster config holds the config lock while node 4 connects. The newer bucket config then
                    // becomes the view, and the reconcile starts over against it after the connect.
                    var clusterUpdate = context.ConfigUpdatedAsync(DockerConfig(2, PlainNode(0, kv: true),
                        PlainNode(1, kv: true), PlainNode(2, query: true), PlainNode(4, query: true)));
                    Task bucketUpdate;
                    try
                    {
                        Assert.False(clusterUpdate.IsCompleted);
                        bucketUpdate = context.ProcessClusterMapAsync(bucket, PlainBucketConfig(3, "default",
                            PlainNode(0, kv: true), PlainNode(1, kv: true), PlainNode(3, query: true)));
                        Assert.False(bucketUpdate.IsCompleted);
                    }
                    finally
                    {
                        connectGate.TrySetResult(true);
                    }

                    await clusterUpdate.WaitAsync(Deadline);
                    await bucketUpdate.WaitAsync(Deadline);
                    break;
            }

            Assert.Equal([PlainHost(0), PlainHost(1), PlainHost(3)],
                context.Nodes.Select(node => node.EndPoint.Host).OrderBy(host => host, StringComparer.Ordinal));
            var query = Assert.Single(created, node => node.EndPoint.Host == PlainHost(3));
            Assert.Null(query.Owner);
        }

        // The first connect of a new query node fails, so the view at rev 2 has no reconcile yet. A copy of that bucket
        // config fetched again, or a cluster config at rev 2, creates the node.
        [Theory]
        [InlineData("bucket")]
        [InlineData("cluster")]
        public async Task Reconcile_AfterAFailedConnect_RunsAgainAtTheSameRevision(string retry)
        {
            var attempts = 0;
            var factory = new Mock<IClusterNodeFactory>(MockBehavior.Strict);
            factory.Setup(f => f.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<NodeAdapter>(),
                    It.IsAny<CancellationToken>()))
                .Returns((HostEndpointWithPort endPoint, NodeAdapter nodeAdapter, CancellationToken _) =>
                    ++attempts == 1
                        ? Task.FromException<IClusterNode>(new SocketException((int)SocketError.ConnectionRefused))
                        : Task.FromResult(CreateGlobalNode(endPoint, nodeAdapter)));
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(factory.Object));
            var initial = DockerConfig(1, PlainNode(0, kv: true), PlainNode(1, kv: true));
            AddGlobalNodes(context, initial);
            context.UpdateClusterNodesConfig(initial);
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            NodesExt[] WithQuery() => [PlainNode(0, kv: true), PlainNode(1, kv: true), PlainNode(3, query: true)];

            var failed = context.ProcessClusterMapAsync(bucket, PlainBucketConfig(2, "default", WithQuery()));
            await Assert.ThrowsAsync<SocketException>(() => AssertCompleted(failed));
            Assert.DoesNotContain(context.Nodes, node => node.EndPoint.Host == PlainHost(3));

            await AssertCompleted(retry == "bucket"
                ? context.ProcessClusterMapAsync(bucket, PlainBucketConfig(2, "default", WithQuery()))
                : context.ConfigUpdatedAsync(DockerConfig(2, WithQuery())));

            Assert.Single(context.Nodes, node => node.EndPoint.Host == PlainHost(3));
            Assert.Equal(2, attempts);
        }

        // NCBC-2422. A bucket config lists a joining KV node in nodesExt before its nodes list holds it. The bucket
        // map drops that entry, but the host already runs KV for the cluster.
        [Fact]
        public async Task ProcessClusterMap_KvNodeMissingFromNodes_GetsAClusterKvNodeThatTheBucketDoesNotClaim()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created)));
            var initial = DockerConfig(1, PlainNode(0, kv: true), PlainNode(1, kv: true));
            AddGlobalNodes(context, initial);
            context.UpdateClusterNodesConfig(initial);
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            var joining = PlainBucketConfig(2, "default", PlainNode(0, kv: true), PlainNode(1, kv: true),
                PlainNode(2, kv: true));
            joining.Nodes.RemoveAt(2);

            await AssertCompleted(context.ProcessClusterMapAsync(bucket, joining));

            Assert.DoesNotContain(joining.GetNodes(), nodeAdapter => nodeAdapter.Hostname == PlainHost(2));
            var node = Assert.Single(created);
            Assert.Equal(new HostEndpointWithPort(PlainHost(2), 11210), node.EndPoint);
            Assert.True(node.HasKv);
            Assert.Null(node.Owner);
            Assert.Single(context.Nodes, clusterNode => clusterNode.EndPoint.Host == PlainHost(2));
            Assert.Equal(11210, joining.NodesExt[2].Services.Kv);
        }

        // During a rebalance configs arrive many times per second. A cluster config at or below the view returns
        // before it builds a node list, so the empty config here does not throw.
        [Theory]
        [InlineData(6UL)]
        [InlineData(7UL)]
        public async Task ConfigUpdated_ClusterConfigAtOrBelowTheView_BuildsNoNodeListAndChangesNoNode(ulong rev)
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created)));
            var initial = DockerConfig(1, PlainNode(0, kv: true), PlainNode(1, kv: true));
            AddGlobalNodes(context, initial);
            context.UpdateClusterNodesConfig(initial);
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                PlainBucketConfig(7, "default", PlainNode(0, kv: true), PlainNode(1, kv: true))));
            var nodes = context.Nodes.ToList();

            var stale = DockerConfig(rev);
            Assert.Throws<ServiceMissingException>(() => stale.GetClusterNodes());
            await AssertCompleted(context.ConfigUpdatedAsync(stale));

            Assert.Same(stale, context.GlobalConfig);
            Assert.Equal(nodes, context.Nodes);
            Assert.Empty(created);
        }

        // A bucket config at or below the view runs no cluster reconcile, so it takes no lock and creates no node with
        // no owner, also when it lists a node that the view lacks.
        [Theory]
        [InlineData(4UL)]
        [InlineData(5UL)]
        public async Task ProcessClusterMap_BucketConfigAtOrBelowTheView_TakesNoLockAndChangesNoNodeWithNoOwner(ulong rev)
        {
            var created = new List<IClusterNode>();
            var connectGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created, gatedHost: PlainHost(4), connectGate: connectGate.Task)));
            var initial = DockerConfig(1, PlainNode(0, kv: true), PlainNode(1, kv: true));
            AddGlobalNodes(context, initial);
            context.UpdateClusterNodesConfig(initial);
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                PlainBucketConfig(5, "default", PlainNode(0, kv: true), PlainNode(1, kv: true))));
            var nodeCount = context.Nodes.Count;

            // The cluster config holds the config lock while node 4 connects.
            var clusterUpdate = context.ConfigUpdatedAsync(DockerConfig(9, PlainNode(0, kv: true),
                PlainNode(1, kv: true), PlainNode(4, query: true)));
            try
            {
                Assert.False(clusterUpdate.IsCompleted);

                await AssertCompleted(context.ProcessClusterMapAsync(bucket, PlainBucketConfig(rev, "default",
                    PlainNode(0, kv: true), PlainNode(1, kv: true), PlainNode(3, query: true))));

                Assert.DoesNotContain(context.Nodes, node => node.EndPoint.Host == PlainHost(3));
                Assert.Equal(nodeCount, context.Nodes.Count);
                Assert.Empty(created);
            }
            finally
            {
                connectGate.TrySetResult(true);
            }

            await clusterUpdate.WaitAsync(Deadline);
        }

        #endregion

        #region Nodes with no owner (NCBC-4423)

        // No bucket owns a node with no K/V service. Once buckets own every K/V node no cluster config arrives, so the
        // bucket configs must remove it.
        [Fact]
        public async Task ProcessClusterMap_Node_With_No_Kv_Left_The_Map_Is_Removed()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created)));
            var nodes = AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, query: true)));
            var query = nodes.Single(node => node.HasQuery);
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                BucketDockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true), DockerNode(2, query: true))));

            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                BucketDockerConfig(2, DockerNode(0, kv: true), DockerNode(1, kv: true))));

            Assert.DoesNotContain(context.Nodes, node => ReferenceEquals(node, query));
            Assert.Throws<ServiceNotAvailableException>(() => context.GetRandomNodeForService(ServiceType.Query));
            Assert.Empty(created);
        }

        // The second bucket lags, so its older config still lists the query node that a newer config removed.
        [Fact]
        public async Task ProcessClusterMap_Lagging_Bucket_Creates_No_Node_With_No_Kv_And_Takes_No_Lock()
        {
            var created = new List<IClusterNode>();
            var connectGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created, gatedHost: DockerHost(3), connectGate: connectGate.Task)));
            AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, query: true)));
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            using var lagging = new LateBucket("lagging", context, Task.CompletedTask, new AsyncCounter());
            NodesExt[] WithQuery() => [DockerNode(0, kv: true), DockerNode(1, kv: true), DockerNode(2, query: true)];
            await AssertCompleted(context.ProcessClusterMapAsync(bucket, BucketDockerConfig(1, WithQuery())));
            await AssertCompleted(context.ProcessClusterMapAsync(lagging, BucketDockerConfig(1, WithQuery(), "lagging")));
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                BucketDockerConfig(3, DockerNode(0, kv: true), DockerNode(1, kv: true))));
            Assert.DoesNotContain(context.Nodes, node => node.HasQuery);
            var nodeCount = context.Nodes.Count;
            var createdCount = created.Count;

            // The cluster config holds the config lock while the new search node connects.
            var clusterUpdate = context.ConfigUpdatedAsync(DockerConfig(4, DockerNode(0, kv: true),
                DockerNode(1, kv: true), DockerNode(3, search: true)));
            try
            {
                Assert.False(clusterUpdate.IsCompleted);

                await AssertCompleted(context.ProcessClusterMapAsync(lagging, BucketDockerConfig(2, WithQuery(), "lagging")));

                Assert.DoesNotContain(context.Nodes, node => node.HasQuery);
                Assert.Equal(nodeCount, context.Nodes.Count);
                Assert.Equal(createdCount, created.Count);
            }
            finally
            {
                connectGate.TrySetResult(true);
            }

            await clusterUpdate.WaitAsync(Deadline);
        }

        [Fact]
        public async Task ProcessClusterMap_Bucket_Config_Older_Than_Cluster_Config_Keeps_Its_New_Node()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created)));
            AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true)));
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                BucketDockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true))));

            await AssertCompleted(context.ConfigUpdatedAsync(DockerConfig(5, DockerNode(0, kv: true),
                DockerNode(1, kv: true), DockerNode(2, query: true))));
            var query = Assert.Single(created);
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                BucketDockerConfig(4, DockerNode(0, kv: true), DockerNode(1, kv: true))));

            Assert.Contains(context.Nodes, node => ReferenceEquals(node, query));
            Assert.Same(query, context.GetRandomNodeForService(ServiceType.Query));
        }

        // The cluster config trails the bucket config by a few revisions. It is still the newest cluster config.
        [Fact]
        public async Task ConfigUpdated_Cluster_Config_Older_Than_A_Bucket_Config_Becomes_GlobalConfig_And_Changes_No_Node()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created)));
            AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true), DockerNode(2, query: true)));
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            var bucketConfig = BucketDockerConfig(5, DockerNode(0, kv: true), DockerNode(1, kv: true));
            await AssertCompleted(context.ProcessClusterMapAsync(bucket, bucketConfig));
            Assert.DoesNotContain(context.Nodes, node => node.HasQuery);
            var nodes = context.Nodes.ToList();

            var older = DockerConfig(4, DockerNode(0, kv: true), DockerNode(1, kv: true), DockerNode(2, query: true),
                DockerNode(3, search: true));
            await AssertCompleted(context.ConfigUpdatedAsync(older));

            Assert.Same(older, context.GlobalConfig);
            Assert.Same(bucketConfig, context.ClusterNodesConfig);
            Assert.Equal(nodes, context.Nodes.ToList());
            Assert.Empty(created);

            var equal = DockerConfig(5, DockerNode(0, kv: true), DockerNode(1, kv: true));
            await AssertCompleted(context.ConfigUpdatedAsync(equal));

            Assert.Same(equal, context.GlobalConfig);
        }

        [Theory]
        [InlineData(3UL)]
        [InlineData(4UL)]
        public async Task ConfigUpdated_Cluster_Config_Not_Newer_Than_GlobalConfig_Changes_Nothing(ulong rev)
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created)));
            AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true)));
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            var bucketConfig = BucketDockerConfig(5, DockerNode(0, kv: true), DockerNode(1, kv: true));
            await AssertCompleted(context.ProcessClusterMapAsync(bucket, bucketConfig));
            var current = DockerConfig(4, DockerNode(0, kv: true), DockerNode(1, kv: true));
            await AssertCompleted(context.ConfigUpdatedAsync(current));
            var nodes = context.Nodes.ToList();

            await AssertCompleted(context.ConfigUpdatedAsync(DockerConfig(rev, DockerNode(0, kv: true),
                DockerNode(1, kv: true), DockerNode(2, query: true))));

            Assert.Same(current, context.GlobalConfig);
            Assert.Same(bucketConfig, context.ClusterNodesConfig);
            Assert.Equal(nodes, context.Nodes.ToList());
            Assert.Empty(created);
        }

        [Fact]
        public async Task BootstrapGlobal_Starts_The_Cluster_Config_Refresher()
        {
            var config = DockerConfig(1, DockerNode(0, kv: true));
            var seed = SeedNode(new HostEndpointWithPort("localhost", DockerKvPort(0)), config);
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(new List<IClusterNode>(), seed.Object)));
            context.Start();
            Assert.False(context.GlobalConfigRefresher.IsStarted);

            await AssertCompleted(context.BootstrapGlobalAsync());

            Assert.True(context.GlobalConfigRefresher.IsStarted);
        }

        // A server before 6.5 has no cluster config.
        [Fact]
        public async Task BootstrapGlobal_With_No_Cluster_Config_Does_Not_Start_The_Cluster_Config_Refresher()
        {
            var seed = new Mock<IClusterNode>();
            seed.SetupAllProperties();
            seed.Setup(node => node.EndPoint).Returns(new HostEndpointWithPort("localhost", DockerKvPort(0)));
            seed.Setup(node => node.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CouchbaseException(new KeyValueErrorContext { Status = ResponseStatus.BucketNotConnected }));
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(new List<IClusterNode>(), seed.Object)));
            context.Start();

            await AssertCompleted(context.BootstrapGlobalAsync());

            Assert.Null(context.GlobalConfig);
            Assert.Contains(seed.Object, context.Nodes);
            Assert.False(context.GlobalConfigRefresher.IsStarted);
        }

        // A DNS SRV move can reach a cluster with lower revisions. The bootstrap replaces the cluster view.
        [Fact]
        public async Task BootstrapGlobal_To_Lower_Revisions_Lets_Bucket_Configs_Remove_Nodes_Again()
        {
            var clusterConfig = DockerConfig(100, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, query: true));
            var seed = new Mock<IClusterNode>();
            seed.SetupAllProperties();
            seed.Setup(node => node.EndPoint).Returns(new HostEndpointWithPort("localhost", DockerKvPort(0)));
            seed.Setup(node => node.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => clusterConfig);
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created, seed.Object)));
            await AssertCompleted(context.BootstrapGlobalAsync());
            context.RemoveAllNodes();

            clusterConfig = DockerConfig(2, DockerNode(0, kv: true), DockerNode(1, kv: true), DockerNode(2, query: true));
            await AssertCompleted(context.BootstrapGlobalAsync());
            var query = Assert.Single(context.Nodes, node => node.HasQuery);
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());

            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                BucketDockerConfig(3, DockerNode(0, kv: true), DockerNode(1, kv: true))));

            Assert.DoesNotContain(context.Nodes, node => ReferenceEquals(node, query));
        }

        // The config drops the query node and adds a search node that fails to connect.
        [Fact]
        public async Task ProcessClusterMap_New_Node_Fails_To_Connect_Still_Removes_The_Node_That_Left()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created, failingHost: DockerHost(3))));
            var nodes = AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, query: true)));
            var query = nodes.Single(node => node.HasQuery);
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                BucketDockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true), DockerNode(2, query: true))));

            await Assert.ThrowsAsync<SocketException>(() => AssertCompleted(context.ProcessClusterMapAsync(bucket,
                BucketDockerConfig(2, DockerNode(0, kv: true), DockerNode(1, kv: true), DockerNode(3, search: true)))));

            Assert.DoesNotContain(context.Nodes, node => ReferenceEquals(node, query));
            Assert.Empty(created);
        }

        // The bucket's nodes list lacks node 2, as for a K/V node that joined and serves no vBucket yet. The bucket map
        // then leaves it out, but nodesExt still lists it.
        [Fact]
        public async Task ProcessClusterMap_Node_Listed_Only_In_NodesExt_Is_Kept()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created)));
            var nodes = AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, kv: true), DockerNode(3, query: true), DockerNode(4, search: true)));
            var joining = nodes[2];
            var query = nodes[3];
            var search = nodes[4];
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket, ServedByTwoNodes(BucketDockerConfig(1,
                DockerNode(0, kv: true), DockerNode(1, kv: true), DockerNode(2, kv: true), DockerNode(3, query: true),
                DockerNode(4, search: true)))));

            var incoming = ServedByTwoNodes(BucketDockerConfig(2, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, kv: true), DockerNode(3, query: true)));
            Assert.DoesNotContain(incoming.GetNodes(), nodeAdapter => nodeAdapter.CanonicalHostname == DockerHost(2));
            await AssertCompleted(context.ProcessClusterMapAsync(bucket, incoming));

            Assert.Contains(context.Nodes, node => ReferenceEquals(node, joining));
            Assert.Contains(context.Nodes, node => ReferenceEquals(node, query));
            Assert.DoesNotContain(context.Nodes, node => ReferenceEquals(node, search));
            Assert.Empty(created);
        }

        // A bootstrap adds its seed before any map entry matches it, and then uses it.
        [Fact]
        public async Task ProcessClusterMap_Node_With_No_Owner_And_No_Adapter_Is_Kept()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created)));
            AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true)));
            var seed = CreateGlobalNode(new HostEndpointWithPort("10.0.0.9", 11210), nodeAdapter: null);
            context.AddNode(seed);
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());

            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                BucketDockerConfig(2, DockerNode(0, kv: true), DockerNode(1, kv: true))));

            Assert.Contains(context.Nodes, node => ReferenceEquals(node, seed));
            Assert.Empty(created);
        }

        // The cluster config connects a new query node. A newer bucket config without that node applies meanwhile.
        [Fact]
        public async Task ConfigUpdated_New_Node_That_A_Newer_Config_Dropped_During_The_Connect_Is_Removed()
        {
            var created = new List<IClusterNode>();
            var connectGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created, gatedHost: PlainHost(2), connectGate: connectGate.Task)));
            AddGlobalNodes(context, DockerConfig(10, PlainNode(0, kv: true), PlainNode(1, kv: true)));
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                PlainBucketConfig(10, "default", PlainNode(0, kv: true), PlainNode(1, kv: true))));

            var clusterUpdate = context.ConfigUpdatedAsync(DockerConfig(12, PlainNode(0, kv: true),
                PlainNode(1, kv: true), PlainNode(2, query: true)));
            try
            {
                Assert.False(clusterUpdate.IsCompleted);
                await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                    PlainBucketConfig(13, "default", PlainNode(0, kv: true), PlainNode(1, kv: true))));
            }
            finally
            {
                connectGate.TrySetResult(true);
            }

            await clusterUpdate.WaitAsync(Deadline);

            Assert.DoesNotContain(context.Nodes, node => node.EndPoint.Host == PlainHost(2));
        }

        // As above, but the newer bucket config lists the node. It waits for the config lock, as the node is missing.
        [Fact]
        public async Task ConfigUpdated_New_Node_That_A_Newer_Config_Lists_During_The_Connect_Is_Kept_Once()
        {
            var created = new List<IClusterNode>();
            var connectGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created, gatedHost: PlainHost(2), connectGate: connectGate.Task)));
            AddGlobalNodes(context, DockerConfig(10, PlainNode(0, kv: true), PlainNode(1, kv: true)));
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                PlainBucketConfig(10, "default", PlainNode(0, kv: true), PlainNode(1, kv: true))));

            var clusterUpdate = context.ConfigUpdatedAsync(DockerConfig(12, PlainNode(0, kv: true),
                PlainNode(1, kv: true), PlainNode(2, query: true)));
            var bucketUpdate = context.ProcessClusterMapAsync(bucket, PlainBucketConfig(13, "default",
                PlainNode(0, kv: true), PlainNode(1, kv: true), PlainNode(2, query: true)));
            try
            {
                Assert.False(clusterUpdate.IsCompleted);
                Assert.False(bucketUpdate.IsCompleted);
            }
            finally
            {
                connectGate.TrySetResult(true);
            }

            await clusterUpdate.WaitAsync(Deadline);
            await bucketUpdate.WaitAsync(Deadline);

            Assert.Single(context.Nodes, node => node.EndPoint.Host == PlainHost(2));
            Assert.Single(created, node => node.EndPoint.Host == PlainHost(2));
        }

        // A bucket config connects a new query node. A newer config of another bucket without that node applies meanwhile.
        [Fact]
        public async Task ProcessClusterMap_New_Node_That_A_Newer_Config_Dropped_During_The_Connect_Is_Removed()
        {
            var created = new List<IClusterNode>();
            var connectGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created, gatedHost: PlainHost(2), connectGate: connectGate.Task)));
            AddGlobalNodes(context, DockerConfig(10, PlainNode(0, kv: true), PlainNode(1, kv: true)));
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            using var other = new LateBucket("other", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                PlainBucketConfig(10, "default", PlainNode(0, kv: true), PlainNode(1, kv: true))));
            await AssertCompleted(context.ProcessClusterMapAsync(other,
                PlainBucketConfig(10, "other", PlainNode(0, kv: true), PlainNode(1, kv: true))));

            var bucketUpdate = context.ProcessClusterMapAsync(bucket, PlainBucketConfig(11, "default",
                PlainNode(0, kv: true), PlainNode(1, kv: true), PlainNode(2, query: true)));
            try
            {
                Assert.False(bucketUpdate.IsCompleted);
                await AssertCompleted(context.ProcessClusterMapAsync(other,
                    PlainBucketConfig(12, "other", PlainNode(0, kv: true), PlainNode(1, kv: true))));
            }
            finally
            {
                connectGate.TrySetResult(true);
            }

            await bucketUpdate.WaitAsync(Deadline);

            Assert.DoesNotContain(context.Nodes, node => node.EndPoint.Host == PlainHost(2));
        }

        [Fact]
        public async Task ProcessClusterMap_Memcached_Config_Neither_Removes_Nor_Moves_The_View()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created)));
            AddGlobalNodes(context, DockerConfig(1, PlainNode(0, kv: true), PlainNode(1, kv: true),
                PlainNode(2, query: true)));
            await AssertCompleted(context.ConfigUpdatedAsync(DockerConfig(5, PlainNode(0, kv: true),
                PlainNode(1, kv: true), PlainNode(2, query: true))));
            using var memcached = new LateBucket("cache", context, Task.CompletedTask, new AsyncCounter(),
                bucketType: BucketType.Memcached);

            await AssertCompleted(context.ProcessClusterMapAsync(memcached,
                PlainBucketConfig(9, "cache", PlainNode(0, kv: true), PlainNode(1, kv: true))));

            Assert.Contains(context.Nodes, node => node.EndPoint.Host == PlainHost(2));

            // The view stayed at rev 5, so a cluster config at rev 7 still applies.
            await AssertCompleted(context.ConfigUpdatedAsync(DockerConfig(7, PlainNode(0, kv: true),
                PlainNode(1, kv: true), PlainNode(2, query: true), PlainNode(3, query: true))));

            Assert.Contains(context.Nodes, node => node.EndPoint.Host == PlainHost(3));
        }

        // The bucket config needs the lock for a new K/V node. A newer cluster config takes the lock first.
        [Fact]
        public async Task ProcessClusterMap_Config_That_Became_Older_While_Waiting_Keeps_The_Cluster_Nodes()
        {
            var created = new List<IClusterNode>();
            var connectGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created, gatedHost: PlainHost(2), connectGate: connectGate.Task)));
            AddGlobalNodes(context, DockerConfig(10, PlainNode(0, kv: true), PlainNode(1, kv: true)));
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                PlainBucketConfig(10, "default", PlainNode(0, kv: true), PlainNode(1, kv: true))));

            var clusterUpdate = context.ConfigUpdatedAsync(DockerConfig(12, PlainNode(0, kv: true),
                PlainNode(1, kv: true), PlainNode(2, query: true)));
            var bucketUpdate = context.ProcessClusterMapAsync(bucket, PlainBucketConfig(11, "default",
                PlainNode(0, kv: true), PlainNode(1, kv: true), PlainNode(3, kv: true)));
            try
            {
                Assert.False(clusterUpdate.IsCompleted);
                Assert.False(bucketUpdate.IsCompleted);
            }
            finally
            {
                connectGate.TrySetResult(true);
            }

            await clusterUpdate.WaitAsync(Deadline);
            await bucketUpdate.WaitAsync(Deadline);

            Assert.Single(context.Nodes, node => node.EndPoint.Host == PlainHost(2));
            Assert.Single(context.Nodes, node => node.EndPoint.Host == PlainHost(3));
        }

        [Fact]
        public async Task ProcessClusterMap_Forced_Config_At_A_Lower_Revision_Removes_And_Becomes_The_View()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created)));
            AddGlobalNodes(context, DockerConfig(1, PlainNode(0, kv: true), PlainNode(1, kv: true),
                PlainNode(2, query: true), PlainNode(3, query: true)));
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ConfigUpdatedAsync(DockerConfig(20, PlainNode(0, kv: true),
                PlainNode(1, kv: true), PlainNode(2, query: true), PlainNode(3, query: true))));

            var forced = PlainBucketConfig(5, "default", PlainNode(0, kv: true), PlainNode(1, kv: true),
                PlainNode(3, query: true));
            forced.IgnoreRev = true;
            await AssertCompleted(context.ProcessClusterMapAsync(bucket, forced));

            Assert.DoesNotContain(context.Nodes, node => node.EndPoint.Host == PlainHost(2));

            // The view is now rev 5, so a config at rev 6 removes a node it does not list.
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                PlainBucketConfig(6, "default", PlainNode(0, kv: true), PlainNode(1, kv: true))));

            Assert.DoesNotContain(context.Nodes, node => node.EndPoint.Host == PlainHost(3));
            Assert.Empty(created);
        }

        [Fact]
        public async Task ProcessClusterMap_Older_Config_Creates_And_Owns_A_New_Kv_Node()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created)));
            AddGlobalNodes(context, DockerConfig(1, PlainNode(0, kv: true), PlainNode(1, kv: true)));
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ConfigUpdatedAsync(DockerConfig(20, PlainNode(0, kv: true),
                PlainNode(1, kv: true))));

            await AssertCompleted(context.ProcessClusterMapAsync(bucket, PlainBucketConfig(10, "default",
                PlainNode(0, kv: true), PlainNode(1, kv: true), PlainNode(2, kv: true))));

            var node = Assert.Single(context.Nodes, node => node.EndPoint.Host == PlainHost(2));
            Assert.Same(bucket, node.Owner);
            Assert.Single(created);
        }

        // The query node moved to a new external host. The map entry has its node id at the new endpoint.
        [Fact]
        public async Task ProcessClusterMap_Node_With_No_Owner_That_Moved_Is_Replaced()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                DockerOptions(NodeFactory(created)));
            var nodes = AddGlobalNodes(context, DockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true),
                DockerNode(2, query: true)));
            var query = nodes.Single(node => node.HasQuery);
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                BucketDockerConfig(1, DockerNode(0, kv: true), DockerNode(1, kv: true), DockerNode(2, query: true))));

            var moved = BucketDockerConfig(2, DockerNode(0, kv: true), DockerNode(1, kv: true), DockerNode(2, query: true));
            moved.NodesExt[2].AlternateAddresses[NetworkResolution.External].Hostname = "query-2.example.com";
            await AssertCompleted(context.ProcessClusterMapAsync(bucket, moved));

            Assert.DoesNotContain(context.Nodes, node => ReferenceEquals(node, query));
            Assert.Single(context.Nodes, node => node.EndPoint.Host == "query-2.example.com");
        }

        // The bucket map comes from the nodes list when nodesExt is shorter, so its entries have no node id.
        [Fact]
        public async Task ProcessClusterMap_Node_Listed_In_A_Shorter_NodesExt_Is_Kept()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created)));
            AddGlobalNodes(context, DockerConfig(1, PlainNode(0, kv: true), PlainNode(1, kv: true),
                PlainNode(2, query: true)));
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket, PlainBucketConfig(1, "default",
                PlainNode(0, kv: true), PlainNode(1, kv: true), PlainNode(2, query: true))));

            var incoming = PlainBucketConfig(2, "default", PlainNode(0, kv: true), PlainNode(1, kv: true),
                PlainNode(2, query: true));
            incoming.Nodes.Add(new Node { Hostname = $"{PlainHost(5)}:8091" });
            incoming.Nodes.Add(new Node { Hostname = $"{PlainHost(6)}:8091" });
            Assert.True(incoming.NodesExt.Count < incoming.Nodes.Count);
            await AssertCompleted(context.ProcessClusterMapAsync(bucket, incoming));

            Assert.Contains(context.Nodes, node => node.EndPoint.Host == PlainHost(2));
        }

        [Fact]
        public async Task ConfigUpdated_Cluster_Config_Older_Than_A_Lock_Free_Bucket_Config_Is_Skipped()
        {
            var created = new List<IClusterNode>();
            using var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(),
                PlainOptions(NodeFactory(created)));
            AddGlobalNodes(context, DockerConfig(1, PlainNode(0, kv: true), PlainNode(1, kv: true)));
            using var bucket = new LateBucket("default", context, Task.CompletedTask, new AsyncCounter());
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                PlainBucketConfig(1, "default", PlainNode(0, kv: true), PlainNode(1, kv: true))));

            // Every node of this config has its node already, so it takes the path with no lock.
            await AssertCompleted(context.ProcessClusterMapAsync(bucket,
                PlainBucketConfig(8, "default", PlainNode(0, kv: true), PlainNode(1, kv: true))));
            await AssertCompleted(context.ConfigUpdatedAsync(DockerConfig(6, PlainNode(0, kv: true),
                PlainNode(1, kv: true), PlainNode(2, query: true))));

            Assert.DoesNotContain(context.Nodes, node => node.EndPoint.Host == PlainHost(2));
            Assert.Empty(created);
        }

        private static string PlainHost(int index) => $"10.1.0.{index + 2}";

        /// <summary>
        /// A node on the default network, with no alternate address.
        /// </summary>
        private static NodesExt PlainNode(int index, bool kv = false, bool query = false) =>
            new()
            {
                Hostname = PlainHost(index),
                Services = new Services { Mgmt = 8091, Kv = kv ? 11210 : 0, N1Ql = query ? 8093 : 0 }
            };

        /// <summary>
        /// A bucket config whose nodes list has the K/V nodes, as the server sends it.
        /// </summary>
        private static BucketConfig PlainBucketConfig(ulong rev, string name, params NodesExt[] nodes)
        {
            var config = BucketDockerConfig(rev, nodes, name);
            config.Nodes = nodes.Where(node => node.Services.Kv > 0)
                .Select(node => new Node { Hostname = $"{node.Hostname}:8091" })
                .ToList();
            return config;
        }

        private static ClusterOptions PlainOptions(IClusterNodeFactory nodeFactory)
        {
            var options = new ClusterOptions().WithConnectionString($"couchbase://{PlainHost(0)}")
                .WithPasswordAuthentication("username", "password");
            options.AddClusterService(nodeFactory);
            return options;
        }

        /// <summary>
        /// Lists only nodes 0 and 1 in the bucket's nodes list.
        /// </summary>
        private static BucketConfig ServedByTwoNodes(BucketConfig config)
        {
            config.Nodes = [new Node { Hostname = $"{DockerHost(0)}:8091" }, new Node { Hostname = $"{DockerHost(1)}:8091" }];
            return config;
        }

        private static BucketConfig BucketDockerConfig(ulong rev, params NodesExt[] nodes) =>
            BucketDockerConfig(rev, nodes, "default");

        private static BucketConfig BucketDockerConfig(ulong rev, NodesExt[] nodes, string name)
        {
            var config = DockerConfig(rev, nodes);
            config.Name = name;
            return config;
        }

        #endregion

        private static IClusterNode CreateGlobalNode(HostEndpointWithPort endPoint, NodeAdapter nodeAdapter,
            IConnectionPool connectionPool = null)
        {
            var mockConnectionPoolFactory = new Mock<IConnectionPoolFactory>();
            mockConnectionPoolFactory
                .Setup(m => m.Create(It.IsAny<ClusterNode>()))
                .Returns(connectionPool ?? new Mock<IConnectionPool>().Object);

            return new ClusterNode(new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password")), mockConnectionPoolFactory.Object,
                new Mock<ILogger<ClusterNode>>().Object,
                new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                new Mock<ICircuitBreaker>().Object,
                new Mock<ISaslMechanismFactory>().Object,
                new Redactor(RedactionLevel.None),
                endPoint,
                nodeAdapter,
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object
            );
        }

        private IClusterNode CreateMockedNode(string hostname, int port, NodeAdapter nodeAdapter = null)
        {
            nodeAdapter ??= new NodeAdapter
            {
                Hostname = hostname,
                KeyValue = port
            };

            var clusterNode = CreateGlobalNode(new HostEndpointWithPort(hostname, port), nodeAdapter);
            clusterNode.Owner = new FakeBucket("default", new ClusterOptions().WithPasswordAuthentication("username", "password"));
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
            mockClusterNode.Setup(cn => cn.GetClusterMap(null, null, It.IsAny<CancellationToken>())).Returns(Task.FromResult(config));

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
