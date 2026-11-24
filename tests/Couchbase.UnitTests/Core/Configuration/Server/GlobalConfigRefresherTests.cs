#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Test.Common.Utils;
using Couchbase.UnitTests.Helpers;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Configuration.Server;

public class GlobalConfigRefresherTests(ITestOutputHelper outputHelper)
{
    // Bounds each wait on the refresher loop, so a regression fails instead of hanging.
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Poll_Waits_On_The_Injected_Clock()
    {
        // A real sleep this long would hang the test.
        var pollInterval = TimeSpan.FromSeconds(30);
        var clock = new SteppableClock();
        using var context = CreateContext(out var configHandler);
        context.ClusterOptions.ConfigPollInterval = pollInterval;

        var node = AddNode(context, "host1", owner: null);
        var polls = new AsyncCounter();
        node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback(() => polls.Increment())
            .ReturnsAsync(new BucketConfig());

        using var refresher = CreateRefresher(context, configHandler, clock);
        refresher.Start();

        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        Assert.Equal(0, polls.Count);

        clock.Advance(pollInterval);
        await polls.WaitForAsync(1).WaitAsync(Deadline);

        // Reaching the next sleep means the pass ran to completion.
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        Assert.Equal(1, polls.Count);
    }

    [Fact]
    public async Task Poll_AsksOnlyTheNodesNoBucketOwns()
    {
        // Once a bucket owns a node, the bucket refresher polls it.
        var pollInterval = TimeSpan.FromSeconds(10);
        var clock = new SteppableClock();
        using var context = CreateContext(out var configHandler);
        context.ClusterOptions.ConfigPollInterval = pollInterval;
        var published = new List<BucketConfig>();
        configHandler.Setup(x => x.Publish(It.IsAny<BucketConfig>())).Callback((BucketConfig c) => published.Add(c));

        var bucketNode = AddNode(context, "host1", owner: Mock.Of<IBucket>(b => b.Name == "default"));
        var globalNode = AddNode(context, "host2", owner: null);
        globalNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BucketConfig { Rev = 2 });

        using var refresher = CreateRefresher(context, configHandler, clock);
        refresher.Start();
        for (var pass = 0; pass < 3; pass++)
        {
            await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
            clock.Advance(pollInterval);
        }

        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        globalNode.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        bucketNode.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.All(published, c => Assert.Equal(BucketConfig.GlobalBucketName, c.Name));
    }

    [Fact]
    public async Task Fetch_WhenKvServesNoConfig_SwitchesEverySubscriberToHttpStreaming()
    {
        var streaming = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var context = CreateContext(out var configHandler);
        configHandler.Setup(x => x.StartHttpStreaming(It.IsAny<string?>()))
            .Callback((string? name) => streaming.TrySetResult(name));
        var node = AddNode(context, "host1", owner: null);
        node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DocumentNotFoundException());

        using var refresher = CreateRefresher(context, configHandler, new SteppableClock());
        refresher.Start();
        refresher.RequestRefresh(new ConfigVersion(1, 3));

        Assert.Null(await streaming.Task.WaitAsync(Deadline));
    }

    [Fact]
    public async Task Poll_WhenBucketsOwnEveryKvNode_DoesNotRebootstrap()
    {
        // The bucket refreshers fetch then, so no node to ask is not a failure.
        var pollInterval = TimeSpan.FromSeconds(10);
        var clock = new SteppableClock();
        var dnsLookups = new AsyncCounter();
        using var context = CreateContext(out var configHandler, options =>
        {
            var dnsResolver = new Mock<IDnsResolver>();
            dnsResolver.Setup(x => x.GetDnsSrvEntriesAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
                .Callback(() => dnsLookups.Increment())
                .ReturnsAsync(Array.Empty<HostEndpoint>());
            options.AddClusterService(dnsResolver.Object);
            options.ConnectionStringValue = new ConnectionString(ConnectionString.Parse("couchbase://example.com"),
                [new HostEndpoint("host1", 11210)], isDnsSrv: true, new Uri("couchbase://example.com"));
            options.ConfigPollInterval = pollInterval;
        });
        AddNode(context, "host1", owner: Mock.Of<IBucket>(b => b.Name == "default"));

        using var refresher = CreateRefresher(context, configHandler, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        clock.Advance(pollInterval);
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        await context.DnsSrvRebootstrap.WaitAsync(Deadline);

        Assert.Equal(0, dnsLookups.Count);
    }

    [Fact]
    public async Task Push_AfterBootstrapToLowerRevisions_IsFetched()
    {
        // A DNS SRV rebootstrap can reach a cluster recreated behind the same name, which starts again at low
        // revisions. Polling is off, so only the push can bring the next config.
        var clock = new SteppableClock();
        var nodeFactory = new Mock<IClusterNodeFactory>();
        using var context = CreateContext(out var configHandler, options =>
        {
            options.WithConnectionString("couchbase://host1:11210");
            options.EnableConfigPolling = false;
            options.AddClusterService<TimeProvider>(clock);
            options.AddClusterService(nodeFactory.Object);
        });
        var published = new List<ConfigVersion>();
        var publishes = new AsyncCounter();
        configHandler.Setup(x => x.Publish(It.IsAny<BucketConfig>())).Callback((BucketConfig config) =>
        {
            published.Add(config.ConfigVersion);
            publishes.Increment();
        });
        var node = AddNode(context, "host1", owner: null);
        var served = Version(100);
        node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(served));
        nodeFactory.Setup(x => x.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(node.Object);
        context.GlobalConfig = Version(99);
        var refresher = context.GlobalConfigRefresher;
        refresher.Start();
        await clock.WaitForSleepAsync(ConfigRefresher.MaxSleep).WaitAsync(Deadline);

        // The old cluster pushes version 100, and the refresher fetches it.
        refresher.RequestRefresh(served.ConfigVersion);
        await clock.WaitForSleepAsync(ConfigRefresher.MaxSleep).WaitAsync(Deadline);

        served = Version(5);
        served.NodesExt = [new NodesExt { Hostname = "host1", Services = new Services { Kv = 11210 } }];
        await context.BootstrapGlobalAsync().WaitAsync(Deadline);
        Assert.Equal(served.ConfigVersion, context.GlobalConfig.ConfigVersion);

        served = Version(6);
        refresher.RequestRefresh(served.ConfigVersion);
        await publishes.WaitForAsync(2).WaitAsync(Deadline);

        Assert.Equal([new ConfigVersion(1, 100), new ConfigVersion(1, 6)], published);
    }

    private static BucketConfig Version(ulong rev)
    {
        var config = new BucketConfig { RevEpoch = 1, Rev = rev };
        config.OnDeserialized();
        return config;
    }

    private static ClusterContext CreateContext(out Mock<IConfigHandler> configHandler,
        Action<ClusterOptions>? configure = null)
    {
        configHandler = new Mock<IConfigHandler>();
        var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
        configure?.Invoke(options);
        options.AddClusterService(configHandler.Object);
        return new ClusterContext(new CancellationTokenSource(), options);
    }

    private static Mock<IClusterNode> AddNode(ClusterContext context, string host, IBucket? owner)
    {
        var node = new Mock<IClusterNode>();
        node.SetupGet(x => x.EndPoint).Returns(new HostEndpointWithPort(host, 11210));
        node.SetupGet(x => x.HasKv).Returns(true);
        node.SetupGet(x => x.KeyEndPoints).Returns(new List<HostEndpointWithPort>());
        node.SetupGet(x => x.ServerFeatures)
            .Returns(new ServerFeatureSet([ServerFeatures.ClustermapChangeNotificationBrief]));
        node.SetupProperty(x => x.Owner, owner);
        context.AddNode(node.Object);
        return node;
    }

    private GlobalConfigRefresher CreateRefresher(ClusterContext context, Mock<IConfigHandler> configHandler,
        TimeProvider clock) =>
        new(context, configHandler.Object, new TestOutputLogger(outputHelper, nameof(GlobalConfigRefresherTests)),
            new Redactor(RedactionLevel.None), clock);
}
