#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
    public async Task Poll_WhileABucketIsOpen_AsksTheClusterNodesOnly()
    {
        var pollInterval = TimeSpan.FromSeconds(10);
        var clock = new SteppableClock();
        using var context = CreateContext(out var configHandler);
        context.ClusterOptions.ConfigPollInterval = pollInterval;
        var published = new List<BucketConfig>();
        configHandler.Setup(x => x.Publish(It.IsAny<BucketConfig>())).Callback((BucketConfig c) => published.Add(c));

        var clusterNodes = new[] { AddNode(context, "host1", owner: null), AddNode(context, "host2", owner: null) };
        foreach (var clusterNode in clusterNodes)
        {
            clusterNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BucketConfig { Rev = 2 });
        }

        var bucket = RegisterBucket(context);
        var bucketNode = new Mock<IClusterNode>();
        bucketNode.SetupGet(x => x.EndPoint).Returns(new HostEndpointWithPort("host1", 11210));
        bucketNode.SetupGet(x => x.KeyEndPoints).Returns(new List<HostEndpointWithPort> { new("host1", 11210) });
        bucketNode.SetupGet(x => x.HasKv).Returns(true);
        bucket.Nodes.Add(bucketNode.Object);

        using var refresher = CreateRefresher(context, configHandler, clock);
        refresher.Start();
        for (var pass = 0; pass < 3; pass++)
        {
            await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
            clock.Advance(pollInterval);
        }

        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        var asked = 0;
        foreach (var clusterNode in clusterNodes)
        {
            asked += clusterNode.Invocations.Count(i => i.Method.Name == nameof(IClusterNode.GetClusterMap));
        }

        Assert.Equal(3, asked);
        bucketNode.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.All(published, c => Assert.Equal(BucketConfig.GlobalBucketName, c.Name));
    }

    [Fact]
    public async Task Fetch_WhenEveryNodeAnswersKeyNotFound_SwitchesEverySubscriberToHttpStreaming()
    {
        var streaming = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var context = CreateContext(out var configHandler);
        configHandler.Setup(x => x.StartHttpStreaming(It.IsAny<string?>()))
            .Callback((string? name) => streaming.TrySetResult(name));
        var nodes = new[] { AddNode(context, "host1", owner: null), AddNode(context, "host2", owner: null) };
        foreach (var node in nodes)
        {
            node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DocumentNotFoundException());
        }

        using var refresher = CreateRefresher(context, configHandler, new SteppableClock());
        refresher.Start();
        refresher.RequestRefresh(new ConfigVersion(1, 3));

        Assert.Null(await streaming.Task.WaitAsync(Deadline));
        foreach (var node in nodes)
        {
            node.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task Fetch_WhenOneNodeAnswersKeyNotFound_PublishesFromAnotherNodeAndGoesOn()
    {
        // The other node is behind the pushed version, so the pass asks both nodes in either order.
        var clock = new SteppableClock();
        var streaming = new AsyncCounter();
        var published = new TaskCompletionSource<BucketConfig>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var context = CreateContext(out var configHandler);
        configHandler.Setup(x => x.StartHttpStreaming(It.IsAny<string?>())).Callback(() => streaming.Increment());
        configHandler.Setup(x => x.Publish(It.IsAny<BucketConfig>())).Callback((BucketConfig c) => published.TrySetResult(c));
        var refusing = AddNode(context, "host1", owner: null);
        refusing.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DocumentNotFoundException());
        AddNode(context, "host2", owner: null)
            .Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Version(3));

        using var refresher = CreateRefresher(context, configHandler, clock);
        refresher.Start();
        refresher.RequestRefresh(new ConfigVersion(1, 4));

        Assert.Equal(new ConfigVersion(1, 3), (await published.Task.WaitAsync(Deadline)).ConfigVersion);

        // The loop goes on and asks again for the pushed version.
        await clock.WaitForSleepAsync(ConfigRefresher.InitialNullConfigRetryDelay).WaitAsync(Deadline);
        refusing.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(0, streaming.Count);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task Fetch_WhenEveryNodeFails_RebootstrapsOnlyWithNoOpenBucket(bool bucketOpen, int expectedLookups)
    {
        // A DNS SRV connection string, so a signal of a failed refresh starts a rebootstrap.
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
            // The rebootstrap then fails at once and does not start the context's own refresher.
            var nodeFactory = new Mock<IClusterNodeFactory>();
            nodeFactory.Setup(x => x.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("No node in this test."));
            options.AddClusterService(nodeFactory.Object);
            options.ConnectionStringValue = new ConnectionString(ConnectionString.Parse("couchbase://example.com"),
                [new HostEndpoint("host1", 11210)], isDnsSrv: true, new Uri("couchbase://example.com"));
            options.ConfigPollInterval = pollInterval;
        });
        AddNode(context, "host1", owner: null)
            .Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());
        using var bucket = new ConfigHandlerTests.FakeBucket(outputHelper, new SemaphoreSlim(0));
        if (bucketOpen)
        {
            context.RegisterBucket(bucket);
        }

        using var refresher = CreateRefresher(context, configHandler, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        clock.Advance(pollInterval);
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        await context.LastRebootstrap.WaitAsync(Deadline);

        Assert.Equal(expectedLookups, dnsLookups.Count);
    }

    // With a bucket open, the bucket refreshers start the rebootstrap, because it also rebuilds every open bucket.
    [Fact]
    public async Task Poll_WhenEveryNodeFailsWhileABucketIsOpen_DoesNotRebootstrap()
    {
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
        var fetches = new AsyncCounter();
        AddNode(context, "host1", owner: null)
            .Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback(() => fetches.Increment())
            .ThrowsAsync(new TimeoutException());
        RegisterBucket(context);

        using var refresher = CreateRefresher(context, configHandler, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        clock.Advance(pollInterval);
        await fetches.WaitForAsync(1).WaitAsync(Deadline);
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        await context.LastRebootstrap.WaitAsync(Deadline);

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

    [Fact]
    public async Task Poll_WithANodeThatHasNotConnected_AsksOnlyTheConnectedNode()
    {
        var pollInterval = TimeSpan.FromSeconds(10);
        var clock = new SteppableClock();
        using var context = CreateContext(out var configHandler);
        context.ClusterOptions.ConfigPollInterval = pollInterval;
        var connected = AddNode(context, "host1", owner: null);
        connected.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BucketConfig)null!);
        var connecting = AddNode(context, "host2", owner: null);
        connecting.SetupGet(x => x.IsAwaitingFirstConnection).Returns(true);

        using var refresher = CreateRefresher(context, configHandler, clock);
        refresher.Start();
        for (var pass = 0; pass < 3; pass++)
        {
            await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
            clock.Advance(pollInterval);
        }
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);

        connected.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        connecting.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PollAndPush_WithOnlyNodesThatHaveNotConnected_FetchNothingSignalNothingAndSleep()
    {
        var pollInterval = TimeSpan.FromSeconds(10);
        var clock = new SteppableClock();
        var dnsLookups = new AsyncCounter();
        using var context = CreateContext(out var configHandler, options => UseDnsSrv(options, dnsLookups, pollInterval));
        var nodes = new[] { AddNode(context, "host1", owner: null), AddNode(context, "host2", owner: null) };
        foreach (var node in nodes)
        {
            node.SetupGet(x => x.IsAwaitingFirstConnection).Returns(true);
        }

        using var refresher = CreateRefresher(context, configHandler, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        clock.Advance(pollInterval);
        // A pass that asks no node waits a full interval for the next poll.
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);

        // A pushed version that no node can be asked for goes through the retry delays, so the loop does not spin.
        refresher.RequestRefresh(new ConfigVersion(1, 3));
        await clock.WaitForSleepAsync(ConfigRefresher.InitialNullConfigRetryDelay).WaitAsync(Deadline);
        clock.Advance(ConfigRefresher.InitialNullConfigRetryDelay);
        await clock.WaitForSleepAsync(ConfigRefresher.GetNullConfigRetryDelay(1)).WaitAsync(Deadline);
        await context.LastRebootstrap.WaitAsync(Deadline);

        foreach (var node in nodes)
        {
            node.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        Assert.Equal(0, dnsLookups.Count);
        // Two loop sleeps and two retry sleeps.
        Assert.Equal(4, clock.TimersCreated);
    }

    [Fact]
    public async Task Poll_WhenANodeThatLostItsConnectionFails_CountsTheFailure()
    {
        var pollInterval = TimeSpan.FromSeconds(10);
        var clock = new SteppableClock();
        var dnsLookups = new AsyncCounter();
        using var context = CreateContext(out var configHandler, options => UseDnsSrv(options, dnsLookups, pollInterval));
        // A node that had a connection reports false, also after it loses it, so its fetch fails.
        var failing = AddNode(context, "host1", owner: null);
        failing.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SocketNotAvailableException("No connection."));
        // It never connected and its connect failed, so it is not asked and counts as a failed fetch. It answers if asked,
        // and an answer would end the failure.
        var connecting = AddNode(context, "host2", owner: null);
        connecting.SetupGet(x => x.IsAwaitingFirstConnection).Returns(true);
        connecting.SetupGet(x => x.HasFailedFirstConnect).Returns(true);
        connecting.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BucketConfig)null!);

        using var refresher = CreateRefresher(context, configHandler, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        clock.Advance(pollInterval);
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        await context.LastRebootstrap.WaitAsync(Deadline);

        failing.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(1, dnsLookups.Count);
    }

    // NCBC-4423. As in the Java SDK, a node whose first connect failed counts as a failed fetch.
    [Fact]
    public async Task Poll_WhenEveryFetchAndEveryFirstConnectFailed_SignalsTheFailure()
    {
        var (context, dnsLookups, fetches) = await RunOnePollAsync(
            asked: [new TimeoutException()],
            awaiting: [true, true]);
        using (context)
        {
            Assert.Equal(1, fetches.Count);
            Assert.Equal(1, dnsLookups.Count);
        }
    }

    // NCBC-4423. A node that is still in its first connect attempt may connect, so the pass cannot fail.
    [Fact]
    public async Task Poll_WhenEveryFetchFailedAndANodeIsInItsFirstConnect_SignalsNothing()
    {
        var (context, dnsLookups, fetches) = await RunOnePollAsync(
            asked: [new TimeoutException()],
            awaiting: [true, false]);
        using (context)
        {
            Assert.Equal(1, fetches.Count);
            Assert.Equal(0, dnsLookups.Count);
        }
    }

    // NCBC-4423. With no node to ask, the nodes whose first connect failed still fail the pass.
    [Fact]
    public async Task Poll_WithNoNodeToAskAndEveryFirstConnectFailed_SignalsTheFailure()
    {
        var (context, dnsLookups, fetches) = await RunOnePollAsync(asked: [], awaiting: [true, true]);
        using (context)
        {
            Assert.Equal(0, fetches.Count);
            Assert.Equal(1, dnsLookups.Count);
        }
    }

    // NCBC-4423
    [Fact]
    public async Task Poll_WhenOneNodeAnswersAndTheOtherFailedItsFirstConnect_SignalsNothing()
    {
        var (context, dnsLookups, fetches) = await RunOnePollAsync(asked: [null], awaiting: [true]);
        using (context)
        {
            Assert.Equal(1, fetches.Count);
            Assert.Equal(0, dnsLookups.Count);
        }
    }

    // NCBC-4423. A pass for a pushed version asks every node until one has that version. A node that answers with
    // nothing newer still answers, so the pass does not fail.
    [Fact]
    public async Task Push_WhenOneNodeIsBehindAndTheOtherFailedItsFirstConnect_SignalsNothing()
    {
        var pollInterval = TimeSpan.FromSeconds(10);
        var clock = new SteppableClock();
        var dnsLookups = new AsyncCounter();
        using var context = CreateContext(out var configHandler, options => UseDnsSrv(options, dnsLookups, pollInterval));
        var behind = AddNode(context, "host0", owner: null);
        behind.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BucketConfig)null!);
        var failed = AddNode(context, "host1", owner: null);
        failed.SetupGet(x => x.IsAwaitingFirstConnection).Returns(true);
        failed.SetupGet(x => x.HasFailedFirstConnect).Returns(true);

        using var refresher = CreateRefresher(context, configHandler, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        refresher.RequestRefresh(new ConfigVersion(1, 3));
        // The pass ran to its end, since it waits to ask again for the pushed version.
        await clock.WaitForSleepAsync(ConfigRefresher.InitialNullConfigRetryDelay).WaitAsync(Deadline);
        await context.LastRebootstrap.WaitAsync(Deadline);

        behind.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(0, dnsLookups.Count);
    }

    /// <summary>
    /// Runs one poll pass on a DNS SRV client, so a failed refresh shows as one DNS SRV lookup.
    /// </summary>
    /// <param name="asked">One connected node per entry. A null entry answers, an exception fails the fetch.</param>
    /// <param name="awaiting">One node per entry that never connected. True when its first connect failed.</param>
    private async Task<(ClusterContext Context, AsyncCounter DnsLookups, AsyncCounter Fetches)> RunOnePollAsync(
        Exception?[] asked, bool[] awaiting)
    {
        var pollInterval = TimeSpan.FromSeconds(10);
        var clock = new SteppableClock();
        var dnsLookups = new AsyncCounter();
        var fetches = new AsyncCounter();
        var context = CreateContext(out var configHandler, options => UseDnsSrv(options, dnsLookups, pollInterval));
        var host = 0;
        foreach (var error in asked)
        {
            var node = AddNode(context, $"host{host++}", owner: null);
            var setup = node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .Callback(() => fetches.Increment());
            if (error is null)
            {
                setup.ReturnsAsync((BucketConfig)null!);
            }
            else
            {
                setup.ThrowsAsync(error);
            }
        }

        foreach (var failed in awaiting)
        {
            var node = AddNode(context, $"host{host++}", owner: null);
            node.SetupGet(x => x.IsAwaitingFirstConnection).Returns(true);
            node.SetupGet(x => x.HasFailedFirstConnect).Returns(failed);
            node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .Callback(() => fetches.Increment())
                .ReturnsAsync((BucketConfig)null!);
        }

        using var refresher = CreateRefresher(context, configHandler, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        clock.Advance(pollInterval);
        // Reaching the next sleep means the pass ran to completion.
        await clock.WaitForSleepAsync(pollInterval).WaitAsync(Deadline);
        await context.LastRebootstrap.WaitAsync(Deadline);
        return (context, dnsLookups, fetches);
    }

    /// <summary>
    /// A DNS SRV connection string, so a signal of a failed refresh starts a rebootstrap. The rebootstrap counts one
    /// lookup and reaches no node.
    /// </summary>
    private static void UseDnsSrv(ClusterOptions options, AsyncCounter dnsLookups, TimeSpan pollInterval)
    {
        var dnsResolver = new Mock<IDnsResolver>();
        dnsResolver.Setup(x => x.GetDnsSrvEntriesAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .Callback(() => dnsLookups.Increment())
            .ReturnsAsync(Array.Empty<HostEndpoint>());
        options.AddClusterService(dnsResolver.Object);
        var nodeFactory = new Mock<IClusterNodeFactory>();
        nodeFactory.Setup(x => x.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No node in this test."));
        options.AddClusterService(nodeFactory.Object);
        options.ConnectionStringValue = new ConnectionString(ConnectionString.Parse("couchbase://example.com"),
            [new HostEndpoint("host1", 11210)], isDnsSrv: true, new Uri("couchbase://example.com"));
        options.ConfigPollInterval = pollInterval;
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

    /// <summary>
    /// Registers an open bucket "default" with no nodes.
    /// </summary>
    private static BucketBase RegisterBucket(ClusterContext context)
    {
        var bucket = new Mock<BucketBase> { CallBase = true };
        bucket.SetupGet(x => x.Name).Returns("default");
        bucket.Setup(x => x.Dispose());
        context.RegisterBucket(bucket.Object);
        return bucket.Object;
    }

    private GlobalConfigRefresher CreateRefresher(ClusterContext context, Mock<IConfigHandler> configHandler,
        TimeProvider clock) =>
        new(context, configHandler.Object, new TestOutputLogger(outputHelper, nameof(GlobalConfigRefresherTests)),
            new Redactor(RedactionLevel.None), clock);
}
