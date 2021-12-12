using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;
using Xunit.Abstractions;
using TraceListener = Couchbase.Core.Diagnostics.Tracing.TraceListener;

namespace Couchbase.UnitTests.Core
{
    public class ClusterContextTests
    {
        private readonly ITestOutputHelper _output;

        public ClusterContextTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task PruneNodesAsync_Removes_Rebalanced_Node()
        {
            //Arrange

            var config = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\config-error.json");
            var context = new ClusterContext();

            var dnsResolver = new Mock<IDnsResolver>();
            var service = new IpEndPointService(dnsResolver.Object, new ClusterOptions());

            var hosts = new List<string>{"10.143.194.101", "10.143.194.102", "10.143.194.103", "10.143.194.104"};
            hosts.ForEach(async x => context.AddNode(await CreateMockedNode(x, 11210, service).ConfigureAwait(false)));

            //Act

            await context.PruneNodesAsync(config).ConfigureAwait(false);

            //Assert

            var removed = await service.GetIpEndPointAsync("10.143.194.102", 11210).ConfigureAwait(false);

            Assert.DoesNotContain(context.Nodes, node => node.EndPoint.Equals(removed));
        }

        [Fact]
        public async Task PruneNodesAsync_Does_Not_Remove_Single_Service_Nodes()
        {
            //Arrange

            var config = ResourceHelper.ReadResource<BucketConfig>(@"Documents\Configs\rev-36310-service-per-node.json");
            var context = new ClusterContext();

            var dnsResolver = new Mock<IDnsResolver>();
            var service = new IpEndPointService(dnsResolver.Object, new ClusterOptions());

            var hosts = new List<string> { "10.143.194.101", "10.143.194.102", "10.143.194.103", "10.143.194.104" };
            hosts.ForEach(async x => context.AddNode(await CreateMockedNode(x, 11210, service).ConfigureAwait(false)));

            //Act

            await context.PruneNodesAsync(config).ConfigureAwait(false);

            //Assert

            foreach (var host in hosts)
            {
                var removed = await service.GetIpEndPointAsync(host, 11210).ConfigureAwait(false);

                Assert.Contains(context.Nodes, node => node.EndPoint.Equals(removed));
            }
        }

        private async Task<IClusterNode> CreateMockedNode(string hostname, int port, IpEndPointService service)
        {
            var mockConnectionPool = new Mock<IConnectionPool>();

            var mockConnectionPoolFactory = new Mock<IConnectionPoolFactory>();
            mockConnectionPoolFactory
                .Setup(m => m.Create(It.IsAny<ClusterNode>()))
                .Returns(mockConnectionPool.Object);

            var clusterNode = new ClusterNode(new ClusterContext(), mockConnectionPoolFactory.Object,
                new Mock<ILogger<ClusterNode>>().Object,
                new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                new Mock<ICircuitBreaker>().Object,
                new Mock<ISaslMechanismFactory>().Object,
                new Mock<IRedactor>().Object,
                await service.GetIpEndPointAsync(hostname, port).ConfigureAwait(false),
                BucketType.Couchbase,
                new NodeAdapter
                {
                    Hostname = hostname,
                    KeyValue = port
                },
                NoopRequestTracer.Instance,
                NoopValueRecorder.Instance
            );

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
        public void When_Tracing_Enabled_Custom_To_CustomTraceListener()
        {
            using var listener = new CustomTraceListener();

            var options = new ClusterOptions();
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

            var activities = listener.Activities.Where(x => x.OperationName == "works").ToArray();

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

            var options = new ClusterOptions();
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
            public HashSet<Activity> Activities { get; } = new();

            public sealed override void Start()
            {
                Listener.ActivityStopped = activity =>
                {
                    // We may be receiving activities from other tests, so lock
                    lock (Activities)
                    {
                        Activities.Add(activity);
                    }
                };
                Listener.SampleUsingParentId = (ref ActivityCreationOptions<string> activityOptions) =>
                    ActivitySamplingResult.AllData;
                Listener.Sample = (ref ActivityCreationOptions<ActivityContext> activityOptions) =>
                    ActivitySamplingResult.AllData;
                Listener.ShouldListenTo = s => true;
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
        }

        #endregion
    }
}
