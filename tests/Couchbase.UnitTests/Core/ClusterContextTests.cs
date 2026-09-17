using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes;
using Couchbase.UnitTests.Utils;
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

        /// <summary>
        /// Regression test: Dispose() used to guard re-entrancy with a plain, non-atomic
        /// `if (_disposed) return; _disposed = true;` check-then-set. Two threads calling Dispose()
        /// concurrently could both pass the check and both run the teardown body, including a
        /// foreach over the non-thread-safe `_ownedObjects` list racing against another thread's
        /// `_ownedObjects.Clear()` - an InvalidOperationException ("Collection was modified").
        /// </summary>
        /// <remarks>
        /// The vulnerable window is exactly two adjacent lines with no yield point between them, so
        /// a black-box test that just fires many concurrent callers and hopes to land inside a
        /// nanosecond-scale window is fundamentally probabilistic: it can pass against the original,
        /// broken implementation on any given run simply because no two callers happened to overlap
        /// that run - an earlier version of this test did exactly that (no synchronization at all,
        /// then a best-effort Barrier, then a "confirm the second caller reached the locking path,
        /// then wait" marker), and all of them could pass on the broken code without ever proving
        /// anything: any marker fired *before* the lock is attempted still leaves a gap between the
        /// signal and the actual attempt, and treating "no second signal within a timeout" as
        /// evidence of exclusion is backwards - it is equally consistent with "descheduled for
        /// longer than the timeout, for reasons that have nothing to do with the lock" (a real risk:
        /// this assembly runs thousands of tests in parallel across classes), which would then let
        /// the first caller finish, set `_disposed`, and let the (merely delayed, not actually
        /// blocked) second caller return early on its own later - passing for the wrong reason.
        ///
        /// This uses <see cref="ClusterContext.DisposeLockContendedHook"/> instead, which closes
        /// that gap structurally: it only fires from inside the same statement that performs a
        /// non-blocking probe of the lock and finds it held, so firing it *is* observing contention,
        /// not a proxy for it. The test then races two mutually exclusive, independently observable
        /// outcomes for the second caller - contended the lock (the fix) vs. reached the same inner
        /// hook the first caller is parked in (the bug, since <see
        /// cref="ClusterContext.DisposeTestHook"/> would then have fired a second time) - and a
        /// timeout where *neither* happens is treated as inconclusive and fails the test outright,
        /// rather than being read as proof either way.
        /// </remarks>
        [Fact]
        public void Dispose_BlocksASecondConcurrentCaller_UntilTheFirstCompletes()
        {
            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            // Enabled with no caller-supplied listener: Start() then owns and adds a
            // ThresholdTraceListener to _ownedObjects, so the teardown under test is the real one,
            // not a no-op over an empty list.
            options.WithThresholdTracing(new ThresholdOptions { Enabled = true });

            var context = new ClusterContext(Mock.Of<ICluster>(), new CancellationTokenSource(), options);
            context.Start();

            var firstEntered = new SemaphoreSlim(0);
            var releaseFirst = new SemaphoreSlim(0);
            var secondReachedInnerHook = new ManualResetEventSlim(false);
            var hookEntries = 0;
            Exception firstException = null;
            Exception secondException = null;

            context.DisposeTestHook = () =>
            {
                var entryNumber = Interlocked.Increment(ref hookEntries);
                if (entryNumber == 1)
                {
                    firstEntered.Release();
                }
                else
                {
                    // A second (or later) caller reached the very section the first is parked in -
                    // the regression this test exists to catch.
                    secondReachedInnerHook.Set();
                }
                // Held here so the test has a real window to observe whether a second caller can
                // also reach this point while the first has not yet finished.
                releaseFirst.Wait();
            };

            // Background, so a bug in this test's own cleanup can never be the thing that hangs
            // the whole test process - only the try/finally below is relied on for that, but this
            // is a cheap second line of defense.
            var first = new Thread(() =>
            {
                try { context.Dispose(); } catch (Exception ex) { firstException = ex; }
            }) { IsBackground = true };
            first.Start();

            Thread second = null;
            var firstJoined = false;
            var secondJoined = true;
            try
            {
                // Confirm the first caller is genuinely inside the critical section before
                // starting the second - otherwise "the second call hasn't gotten here yet" would
                // prove nothing.
                Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(5)),
                    "The first Dispose() call never reached DisposeTestHook.");

                // Wired up only after the first caller is confirmed inside the critical section, so
                // any firing from here on is unambiguously the second caller's own probe finding
                // the lock genuinely held.
                var secondContendedLock = new ManualResetEventSlim(false);
                context.DisposeLockContendedHook = () => secondContendedLock.Set();

                second = new Thread(() =>
                {
                    try { context.Dispose(); } catch (Exception ex) { secondException = ex; }
                }) { IsBackground = true };
                second.Start();

                // Race the two possible, mutually exclusive, independently observable outcomes -
                // do not treat a timeout as evidence of exclusion either way.
                var signaledIndex = WaitHandle.WaitAny(
                    new WaitHandle[] { secondContendedLock.WaitHandle, secondReachedInnerHook.WaitHandle },
                    TimeSpan.FromSeconds(5));

                Assert.True(signaledIndex != WaitHandle.WaitTimeout,
                    "Neither the second caller's lock-contention probe nor a second entry into " +
                    "DisposeTestHook fired within the timeout. That's inconclusive about whether " +
                    "the lock actually serializes these callers, so it must fail this test rather " +
                    "than pass it.");

                Assert.False(secondReachedInnerHook.IsSet,
                    "A second, concurrently-arriving Dispose() call reached the same disposal " +
                    "critical section as the first, instead of contending on the lock the first " +
                    "caller holds - it must be blocked until the first completes.");

                Assert.True(secondContendedLock.IsSet,
                    "The second Dispose() call must have contended on the lock (confirmed via its " +
                    "own non-blocking probe) for this to count as a pass.");

                Assert.Equal(1, hookEntries);
            }
            finally
            {
                // Always let go of whichever caller(s) are parked in the hook. Without this, a
                // failed assertion above would leave the first thread blocked in
                // releaseFirst.Wait() forever - and since these are ordinary (non-background, but
                // see IsBackground above) threads, that can hang the whole test process rather
                // than just failing this test. Releasing more than needed is harmless.
                releaseFirst.Release(2);

                firstJoined = first.Join(TimeSpan.FromSeconds(5));
                if (second is not null)
                {
                    secondJoined = second.Join(TimeSpan.FromSeconds(5));
                }
            }

            // Only reached when the assertions above passed - surfaces problems in the cleanup
            // itself (a thread that didn't finish, or one whose Dispose() call unexpectedly threw)
            // as clear failures rather than silently ignoring them or hanging.
            Assert.True(firstJoined, "The first Dispose() call's thread did not finish after being released.");
            Assert.True(secondJoined, "The second Dispose() call's thread did not finish after being released.");
            Assert.Null(firstException);
            Assert.Null(secondException);
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
