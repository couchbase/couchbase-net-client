#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.Test.Common.Utils;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Configuration.Server;

public class BucketConfigRefresherTests(ITestOutputHelper outputHelper)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Push_ServerVersionRegressed()
    {
        // server pushes (1,3), but returns (1,1)
        var initialBucketConfig = new BucketConfig() { RevEpoch = 1, Rev = 2 };
        initialBucketConfig.OnDeserialized(); // Required to properly initialize ConfigVersion
        var publishes = new AsyncCounter();
        var publishTcs = new TaskCompletionSource<BucketConfig?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Only recorded here, never asserted here: this runs on the refresher's own loop, which swallows
        // exceptions, so a failed assertion would be lost and the test would hang.
        var mockBucket = CreateBucketMock(initialConfig: initialBucketConfig, onPublish: bc =>
        {
            publishes.Increment();
            publishTcs.TrySetResult(bc);
        });
        using ClusterContext mockContext = mockBucket.Context;
        var mockNode = new Mock<IClusterNode>();
        BucketConfig getClusterMapResult = new BucketConfig() { RevEpoch = 1, Rev = 1 };
        getClusterMapResult.OnDeserialized();
        IReadOnlyCollection<HostEndpointWithPort> endpoints = new List<HostEndpointWithPort>();
        var fetches = new AsyncCounter();
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(getClusterMapResult))
            .Callback(() => fetches.Increment());
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockBucket.Nodes.Add(mockNode.Object);
        using var refresher = CreateRefresher(mockBucket, TimeProvider.System);
        refresher.Start();
        var pushedVersion = new ConfigVersion(1, 3);
        refresher.RequestRefresh(pushedVersion);

        // While the server returns an older version than was pushed, the refresher must not publish it.
        // It asks again with a backoff in that case, so a second fetch means the first pass ran
        // to completion. There is nothing to wait and see, and no deadline to lose on a busy runner.
        await fetches.WaitForAsync(2);
        Assert.Equal(0, publishes.Count);

        // Update the version of the config that is returned. This should result in a publish.
        getClusterMapResult.Rev = 3;
        getClusterMapResult.OnDeserialized();

        // No timeout: a refresher which never publishes hangs the test, which is diagnosable, rather
        // than failing an assertion that says nothing about why.
        var publishedConfig = await publishTcs.Task;

        Assert.NotNull(publishedConfig);
        Assert.Equal(pushedVersion, publishedConfig!.ConfigVersion);
    }

    [Fact]
    public async Task Push_ServerReturnsNullThenConfig_AsksAgainAndPublishes()
    {
        // The server pushes (1,3), but the node asked has nothing newer than (1,2) and returns null.
        // The same version is not pushed again, so the refresher must ask again by itself.
        var initialBucketConfig = new BucketConfig() { RevEpoch = 1, Rev = 2 };
        initialBucketConfig.OnDeserialized();
        var publishTcs = new TaskCompletionSource<BucketConfig?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockBucket = CreateBucketMock(initialConfig: initialBucketConfig, onPublish: bc => publishTcs.TrySetResult(bc));
        using ClusterContext mockContext = mockBucket.Context;
        var mockNode = new Mock<IClusterNode>();
        var newConfig = new BucketConfig() { RevEpoch = 1, Rev = 3 };
        newConfig.OnDeserialized();
        IReadOnlyCollection<HostEndpointWithPort> endpoints = new List<HostEndpointWithPort>();
        var fetches = new AsyncCounter();
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<BucketConfig>(fetches.Count < 3 ? null! : newConfig))
            .Callback(() => fetches.Increment());
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockBucket.Nodes.Add(mockNode.Object);
        using var refresher = CreateRefresher(mockBucket, TimeProvider.System);
        refresher.Start();

        refresher.RequestRefresh(new ConfigVersion(1, 3));

        // No timeout: a refresher which gives up after a null answer hangs the test, which is diagnosable.
        var publishedConfig = await publishTcs.Task;

        Assert.NotNull(publishedConfig);
        Assert.Equal(newConfig.ConfigVersion, publishedConfig!.ConfigVersion);
        // Three nulls, then the config.
        Assert.Equal(4, fetches.Count);
    }

    [Fact]
    public async Task Push_FetchThrowsOnOneNode_PublishesFromAnotherNode()
    {
        // The server pushes (1,3). One node times out on every fetch. The other has nothing newer at first,
        // so the first pass asks both nodes in either shuffle order, and the config comes on a later pass.
        var initialBucketConfig = new BucketConfig() { RevEpoch = 1, Rev = 2 };
        initialBucketConfig.OnDeserialized();
        var publishes = new AsyncCounter();
        var publishTcs = new TaskCompletionSource<BucketConfig?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockBucket = CreateBucketMock(initialConfig: initialBucketConfig, onPublish: bc =>
        {
            publishes.Increment();
            publishTcs.TrySetResult(bc);
        });
        using ClusterContext mockContext = mockBucket.Context;
        var newConfig = new BucketConfig() { RevEpoch = 1, Rev = 3 };
        newConfig.OnDeserialized();
        IReadOnlyCollection<HostEndpointWithPort> endpoints = new List<HostEndpointWithPort>();

        var failingNode = new Mock<IClusterNode>();
        var failingFetches = new AsyncCounter();
        failingNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromException<BucketConfig>(new UnambiguousTimeoutException("The request has timed out.")))
            .Callback(() => failingFetches.Increment());

        var workingNode = new Mock<IClusterNode>();
        var workingFetches = new AsyncCounter();
        workingNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(workingFetches.Count < 1 ? null! : newConfig))
            .Callback(() => workingFetches.Increment());

        foreach (var mockNode in new[] { failingNode, workingNode })
        {
            mockNode.SetupGet(x => x.IsDead).Returns(false);
            mockNode.SetupGet(x => x.HasKv).Returns(true);
            mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
            mockBucket.Nodes.Add(mockNode.Object);
        }

        using var refresher = CreateRefresher(mockBucket, TimeProvider.System);
        refresher.Start();

        refresher.RequestRefresh(new ConfigVersion(1, 3));

        // No timeout. A refresher that stops on the failed node hangs the test, which is easy to diagnose.
        var publishedConfig = await publishTcs.Task;

        Assert.NotNull(publishedConfig);
        Assert.Equal(newConfig.ConfigVersion, publishedConfig!.ConfigVersion);
        Assert.Equal(1, publishes.Count);
        Assert.Equal(2, workingFetches.Count);
        // Two when the second pass also asks the failing node first.
        Assert.InRange(failingFetches.Count, 1, 2);
    }

    [Fact]
    public async Task Push_FetchThrowsThenReturnsConfig_AsksAgainAndPublishes()
    {
        // The server pushes (1,3), but the first fetch times out. The same version is not pushed again,
        // so the refresher must ask again by itself.
        var initialBucketConfig = new BucketConfig() { RevEpoch = 1, Rev = 2 };
        initialBucketConfig.OnDeserialized();
        var publishTcs = new TaskCompletionSource<BucketConfig?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockBucket = CreateBucketMock(initialConfig: initialBucketConfig, onPublish: bc => publishTcs.TrySetResult(bc));
        using ClusterContext mockContext = mockBucket.Context;
        var mockNode = new Mock<IClusterNode>();
        var newConfig = new BucketConfig() { RevEpoch = 1, Rev = 3 };
        newConfig.OnDeserialized();
        IReadOnlyCollection<HostEndpointWithPort> endpoints = new List<HostEndpointWithPort>();
        var fetches = new AsyncCounter();
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(() => fetches.Count < 1
                ? Task.FromException<BucketConfig>(new UnambiguousTimeoutException("The request has timed out."))
                : Task.FromResult(newConfig))
            .Callback(() => fetches.Increment());
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockBucket.Nodes.Add(mockNode.Object);
        using var refresher = CreateRefresher(mockBucket, TimeProvider.System);
        refresher.Start();

        refresher.RequestRefresh(new ConfigVersion(1, 3));

        // No timeout. A refresher that gives up after a failed fetch hangs the test, which is easy to diagnose.
        var publishedConfig = await publishTcs.Task;

        Assert.NotNull(publishedConfig);
        Assert.Equal(newConfig.ConfigVersion, publishedConfig!.ConfigVersion);
        // One failure, then the config.
        Assert.Equal(2, fetches.Count);
    }

    [Fact]
    public async Task Dispose_DuringFetch_StopsFetching()
    {
        // The refresher is disposed while a fetch waits on its token. It must not go on to the next node.
        var initialBucketConfig = new BucketConfig() { RevEpoch = 1, Rev = 2 };
        initialBucketConfig.OnDeserialized();
        var mockBucket = CreateBucketMock(initialConfig: initialBucketConfig);
        using ClusterContext mockContext = mockBucket.Context;
        var newConfig = new BucketConfig() { RevEpoch = 1, Rev = 3 };
        newConfig.OnDeserialized();
        IReadOnlyCollection<HostEndpointWithPort> endpoints = new List<HostEndpointWithPort>();
        var fetches = new AsyncCounter();
        for (var i = 0; i < 2; i++)
        {
            var mockNode = new Mock<IClusterNode>();
            mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .Returns(async (ConfigVersion? _, TimeSpan? _, CancellationToken token) =>
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return newConfig;
                })
                .Callback(() => fetches.Increment());
            mockNode.SetupGet(x => x.IsDead).Returns(false);
            mockNode.SetupGet(x => x.HasKv).Returns(true);
            mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
            mockBucket.Nodes.Add(mockNode.Object);
        }

        using var refresher = CreateRefresher(mockBucket, TimeProvider.System);
        refresher.Start();

        refresher.RequestRefresh(new ConfigVersion(1, 3));
        await fetches.WaitForAsync(1);

        refresher.Dispose();

        // Give a refresher which goes on to the next node time to do so.
        await Task.Delay(100);

        Assert.Equal(1, fetches.Count);
    }

    [Fact]
    public async Task Dispose_DuringRetryDelay_LeavesLoopWithoutWarning()
    {
        // The node has nothing newer, so the refresher waits for a retry on a clock that never moves.
        // Dispose must still end the loop, and shutdown is not a warning.
        var initialBucketConfig = new BucketConfig() { RevEpoch = 1, Rev = 2 };
        initialBucketConfig.OnDeserialized();
        var mockBucket = CreateBucketMock(initialConfig: initialBucketConfig);
        using ClusterContext mockContext = mockBucket.Context;
        IReadOnlyCollection<HostEndpointWithPort> endpoints = new List<HostEndpointWithPort>();
        var mockNode = new Mock<IClusterNode>();
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BucketConfig)null!);
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockBucket.Nodes.Add(mockNode.Object);
        var clock = new SteppableClock();
        var logger = new LoopExitLogger();
        using var refresher = CreateRefresher(mockBucket, clock, logger);
        refresher.Start();

        refresher.RequestRefresh(new ConfigVersion(1, 3));
        await clock.WaitForSleepAsync(ConfigRefresher.InitialNullConfigRetryDelay);

        refresher.Dispose();

        // No timeout. A loop that stays parked on the clock hangs the test, which is easy to diagnose.
        await logger.Exited;
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void GetNullConfigRetryDelay_DoublesUpToTheMaximum()
    {
        Assert.Equal(ConfigRefresher.InitialNullConfigRetryDelay, ConfigRefresher.GetNullConfigRetryDelay(0));
        Assert.Equal(TimeSpan.FromTicks(ConfigRefresher.InitialNullConfigRetryDelay.Ticks * 2), ConfigRefresher.GetNullConfigRetryDelay(1));
        Assert.Equal(ConfigRefresher.MaxNullConfigRetryDelay, ConfigRefresher.GetNullConfigRetryDelay(ConfigRefresher.MaxNullConfigRetries));
        Assert.Equal(ConfigRefresher.MaxNullConfigRetryDelay, ConfigRefresher.GetNullConfigRetryDelay(int.MaxValue));
    }

    [Fact]
    public void NullConfigRetryTracker_StopsAfterTheMaximumRetries()
    {
        var tracker = new ConfigRefresher.NullConfigRetryTracker();
        var pushed = new ConfigVersion(1, 3);
        var effective = new ConfigVersion(1, 2);

        for (var i = 0; i < ConfigRefresher.MaxNullConfigRetries; i++)
        {
            Assert.True(tracker.TryNextRetry(pushed, effective, out _));
        }

        Assert.False(tracker.TryNextRetry(pushed, effective, out _));
        Assert.Equal(ConfigRefresher.MaxNullConfigRetries, tracker.Retries);
    }

    [Fact]
    public void NullConfigRetryTracker_ExternalPublishRestartsTheCount()
    {
        // Another refresher publishes a config that moves this bucket's effective version, but it is still
        // below the pushed version. The retries then start again.
        var tracker = new ConfigRefresher.NullConfigRetryTracker();
        var pushed = new ConfigVersion(1, 5);

        for (var i = 0; i < ConfigRefresher.MaxNullConfigRetries; i++)
        {
            Assert.True(tracker.TryNextRetry(pushed, new ConfigVersion(1, 2), out _));
        }

        Assert.False(tracker.TryNextRetry(pushed, new ConfigVersion(1, 2), out _));
        Assert.True(tracker.TryNextRetry(pushed, new ConfigVersion(1, 3), out var delay));
        Assert.Equal(1, tracker.Retries);
        Assert.Equal(ConfigRefresher.InitialNullConfigRetryDelay, delay);
    }

    [Fact]
    public void NullConfigRetryTracker_NewPushedVersionRestartsTheCount()
    {
        var tracker = new ConfigRefresher.NullConfigRetryTracker();
        var effective = new ConfigVersion(1, 2);

        for (var i = 0; i < ConfigRefresher.MaxNullConfigRetries; i++)
        {
            tracker.TryNextRetry(new ConfigVersion(1, 3), effective, out _);
        }

        Assert.True(tracker.TryNextRetry(new ConfigVersion(1, 4), effective, out _));
        Assert.Equal(1, tracker.Retries);
    }

    [Fact]
    public void NullConfigRetryTracker_ResetStartsTheCountAgain()
    {
        var tracker = new ConfigRefresher.NullConfigRetryTracker();
        var pushed = new ConfigVersion(1, 3);
        ConfigVersion? effective = null;

        for (var i = 0; i < ConfigRefresher.MaxNullConfigRetries; i++)
        {
            tracker.TryNextRetry(pushed, effective, out _);
        }

        tracker.Reset();

        Assert.True(tracker.TryNextRetry(pushed, effective, out _));
        Assert.Equal(1, tracker.Retries);
    }

    [Fact]
    public async Task Wake_WithNothingDue_KeepsProcessingPushes()
    {
        // A wake-up can find nothing to do, for example after the bucket applied a config. The refresher must then
        // sleep again and still take the next push.
        var clock = new SteppableClock();
        var publishTcs = new TaskCompletionSource<BucketConfig?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bucket = CreateBucketMock(initialConfig: Version(2), onPublish: bc => publishTcs.TrySetResult(bc));
        using var context = bucket.Context;
        var node = AddNode(bucket, "node-0", push: true);
        node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Version(3));
        using var refresher = CreateRefresher(bucket, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(PollInterval);

        refresher.Reschedule();
        await clock.WaitForSleepAsync(PollInterval);
        node.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);

        refresher.RequestRefresh(new ConfigVersion(1, 3));

        var publishedConfig = await publishTcs.Task;
        Assert.Equal(new ConfigVersion(1, 3), publishedConfig!.ConfigVersion);
    }

    [Fact]
    public async Task Push_ForNewerVersion_Publishes()
    {
        // server pushes (1,2), and returns (1,2)
        var initialBucketConfig = new BucketConfig()
            { RevEpoch = 1, Rev = 1 };
        var publishTcs = new TaskCompletionSource<BucketConfig?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockBucket = CreateBucketMock(
            initialConfig: initialBucketConfig, onPublish: bc => publishTcs.TrySetResult(bc));

        using ClusterContext mockContext = mockBucket.Context;
        var mockNode = new Mock<IClusterNode>();
        BucketConfig getClusterMapResult = new BucketConfig()
            { RevEpoch = 1, Rev = 2 };
        getClusterMapResult.OnDeserialized();
        IReadOnlyCollection<HostEndpointWithPort> endpoints =
            new List<HostEndpointWithPort>();
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(getClusterMapResult));
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockBucket.Nodes.Add(mockNode.Object);
        using var refresher = CreateRefresher(mockBucket, TimeProvider.System);
        refresher.Start();
        refresher.RequestRefresh(new ConfigVersion(1, 2));

        // Awaited with no timeout, and asserted out here rather than inside the publish callback: the
        // callback runs on the refresher's loop, which swallows exceptions, so an assertion made there
        // would be lost.
        var publishedConfig = await publishTcs.Task;

        Assert.NotNull(publishedConfig);
        Assert.Equal(expected: getClusterMapResult.ConfigVersion,
            actual: publishedConfig!.ConfigVersion);
    }

    [Fact]
    public async Task Push_WhenNodesReturnAnOlderVersion_AsksEveryNodeInOnePass()
    {
        // Both nodes lag behind the pushed version, so the pass asks both before it waits to ask again.
        var clock = new SteppableClock();
        var bucket = CreateBucketMock(initialConfig: Version(2));
        using var context = bucket.Context;
        var nodes = new[] { AddNode(bucket, "node-0", push: true), AddNode(bucket, "node-1", push: true) };
        foreach (var node in nodes)
        {
            node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Version(3));
        }

        using var refresher = CreateRefresher(bucket, clock);
        refresher.Start();

        refresher.RequestRefresh(new ConfigVersion(1, 4));
        await clock.WaitForSleepAsync(ConfigRefresher.InitialNullConfigRetryDelay);

        foreach (var node in nodes)
        {
            node.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task Poll_WithPushNegotiated_RunsWhenDueAndNotBefore()
    {
        // Every node negotiated brief push, and the bucket still polls them.
        var clock = new SteppableClock();
        var bucket = CreateBucketMock(initialConfig: Version(2));
        using var context = bucket.Context;
        var node = AddNode(bucket, "node-0", push: true);
        var fetches = new AsyncCounter();
        node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback(() => fetches.Increment())
            .ReturnsAsync((BucketConfig)null!);
        using var refresher = CreateRefresher(bucket, clock);

        // The loop sleeps one full interval before the first poll.
        refresher.Start();
        await clock.WaitForSleepAsync(PollInterval);
        Assert.Equal(0, fetches.Count);

        clock.Advance(PollInterval);

        // Reaching the next sleep means the pass ran to completion.
        await clock.WaitForSleepAsync(PollInterval);
        Assert.Equal(1, fetches.Count);
        node.Verify(x => x.GetClusterMap(Version(2).ConfigVersion, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Push_ResetsThePollTimer()
    {
        var clock = new SteppableClock();
        var bucket = CreateBucketMock(initialConfig: Version(2));
        using var context = bucket.Context;
        var node = AddNode(bucket, "node-0", push: true);
        node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Version(3));
        using var refresher = CreateRefresher(bucket, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(PollInterval);

        clock.Advance(TimeSpan.FromSeconds(4));
        refresher.RequestRefresh(new ConfigVersion(1, 3));

        // A full interval from the push, not the 6 s that were left.
        await clock.WaitForSleepAsync(PollInterval);
    }

    [Fact]
    public async Task Push_WithNothingNewer_ResetsThePollTimer()
    {
        // The node has nothing newer for every fetch of the pushed version. That is still an answer, so after the
        // last retry the next poll is a full interval away.
        var clock = new SteppableClock();
        var bucket = CreateBucketMock(initialConfig: Version(2));
        using var context = bucket.Context;
        var node = AddNode(bucket, "node-0", push: true);
        node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BucketConfig)null!);
        using var refresher = CreateRefresher(bucket, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(PollInterval);

        clock.Advance(TimeSpan.FromSeconds(4));
        refresher.RequestRefresh(new ConfigVersion(1, 3));
        await StepThroughRetriesAsync(clock, ConfigRefresher.MaxNullConfigRetries);

        await clock.WaitForSleepAsync(PollInterval);
    }

    [Fact]
    public async Task Push_WhereEveryFetchFails_KeepsThePollTimer()
    {
        // No fetch of the pushed version gets an answer, so the poll stays due one interval after the start.
        var clock = new SteppableClock();
        var start = clock.GetUtcNow();
        var bucket = CreateBucketMock(initialConfig: Version(2));
        using var context = bucket.Context;
        var node = AddNode(bucket, "node-0", push: true);
        node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnambiguousTimeoutException("The request has timed out."));
        using var refresher = CreateRefresher(bucket, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(PollInterval);

        clock.Advance(TimeSpan.FromSeconds(4));
        refresher.RequestRefresh(new ConfigVersion(1, 3));

        // The retries until 9.63 s, then the poll at 10 s comes before the next retry.
        await StepThroughRetriesAsync(clock, 16);
        await clock.WaitForSleepAsync(start + PollInterval - clock.GetUtcNow());
    }

    [Fact]
    public async Task Poll_WhenANodeFails_AsksTheNextNode()
    {
        var clock = new SteppableClock();
        var bucket = CreateBucketMock(initialConfig: Version(2));
        using var context = bucket.Context;
        var nodes = new[] { AddNode(bucket, "node-0", push: true), AddNode(bucket, "node-1", push: true) };
        foreach (var node in nodes)
        {
            node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnambiguousTimeoutException("The request has timed out."));
        }

        using var refresher = CreateRefresher(bucket, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(PollInterval);

        clock.Advance(PollInterval);
        await clock.WaitForSleepAsync(PollInterval);

        // In either order, one pass asks both nodes.
        foreach (var node in nodes)
        {
            node.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task Poll_WhenEveryNodeFails_RebootstrapsFromDnsSrvOnce()
    {
        var clock = new SteppableClock();
        var dnsLookups = new AsyncCounter();
        var bucket = CreateBucketMock(initialConfig: Version(2), configure: options => UseDnsSrv(options, dnsLookups));
        using var context = bucket.Context;
        foreach (var node in new[] { AddNode(bucket, "node-0", push: true), AddNode(bucket, "node-1", push: true) })
        {
            node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnambiguousTimeoutException("The request has timed out."));
        }

        using var refresher = CreateRefresher(bucket, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(PollInterval);

        clock.Advance(PollInterval);
        await clock.WaitForSleepAsync(PollInterval);
        Assert.Equal(1, dnsLookups.Count);

        clock.Advance(PollInterval);
        await clock.WaitForSleepAsync(PollInterval);
        Assert.Equal(2, dnsLookups.Count);
    }

    [Fact]
    public async Task Poll_WhenEveryNodeIsDead_RebootstrapsFromDnsSrv()
    {
        var clock = new SteppableClock();
        var dnsLookups = new AsyncCounter();
        var bucket = CreateBucketMock(initialConfig: Version(2), configure: options => UseDnsSrv(options, dnsLookups));
        using var context = bucket.Context;
        foreach (var node in new[] { AddNode(bucket, "node-0", push: true), AddNode(bucket, "node-1", push: true) })
        {
            node.SetupGet(x => x.IsDead).Returns(true);
        }

        using var refresher = CreateRefresher(bucket, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(PollInterval);

        clock.Advance(PollInterval);
        await clock.WaitForSleepAsync(PollInterval);

        Assert.Equal(1, dnsLookups.Count);
    }

    [Fact]
    public async Task Poll_WithPushNegotiatedAndPollingWithPushOff_DoesNotFetch()
    {
        var clock = new SteppableClock();
        var bucket = CreateBucketMock(initialConfig: Version(2),
            configure: options => options.Experiments.EnablePollingWithPushConfig = false);
        using var context = bucket.Context;
        var node = AddNode(bucket, "node-0", push: true);
        using var refresher = CreateRefresher(bucket, clock);
        refresher.Start();

        for (var i = 0; i < 3; i++)
        {
            await clock.WaitForSleepAsync(PollInterval);
            clock.Advance(PollInterval);
        }

        await clock.WaitForSleepAsync(PollInterval);
        node.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Poll_WithPushNotNegotiatedAndPollingWithPushOff_Fetches()
    {
        // One node did not negotiate push, so a push can be missing.
        var clock = new SteppableClock();
        var bucket = CreateBucketMock(initialConfig: Version(2),
            configure: options => options.Experiments.EnablePollingWithPushConfig = false);
        using var context = bucket.Context;
        var fetches = new AsyncCounter();
        foreach (var node in new[] { AddNode(bucket, "node-0", push: true), AddNode(bucket, "node-1", push: false) })
        {
            node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .Callback(() => fetches.Increment())
                .ReturnsAsync((BucketConfig)null!);
        }

        using var refresher = CreateRefresher(bucket, clock);
        refresher.Start();
        await clock.WaitForSleepAsync(PollInterval);

        clock.Advance(PollInterval);
        await clock.WaitForSleepAsync(PollInterval);

        Assert.Equal(1, fetches.Count);
    }

    [Fact]
    public async Task Poll_DuringRebalance_RunsEverySecond()
    {
        var clock = new SteppableClock();
        var rebalancing = Version(2);
        rebalancing.VBucketServerMap = new VBucketServerMapDto { VBucketMapForward = [[0, 1]] };
        var bucket = CreateBucketMock(initialConfig: rebalancing);
        using var context = bucket.Context;
        var node = AddNode(bucket, "node-0", push: true);
        var fetches = new AsyncCounter();
        node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback(() => fetches.Increment())
            .ReturnsAsync((BucketConfig)null!);
        using var refresher = CreateRefresher(bucket, clock);

        refresher.Start();
        await clock.WaitForSleepAsync(BucketConfigRefresher.RebalancePollInterval);

        clock.Advance(BucketConfigRefresher.RebalancePollInterval);
        await clock.WaitForSleepAsync(BucketConfigRefresher.RebalancePollInterval);
        Assert.Equal(1, fetches.Count);
    }

    [Fact]
    public async Task Poll_WhenARebalanceStartsMidSleep_RunsWithinASecond()
    {
        // A real bucket, so applying a config is what wakes the refresher.
        var clock = new SteppableClock();
        var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
        options.ConfigPollInterval = PollInterval;
        options.AddClusterService(new Mock<IConfigHandler>().Object);
        using var context = new ClusterContext(new CancellationTokenSource(), options);
        var refresherFactory = new Mock<IConfigRefresherFactory>();
        refresherFactory.Setup(x => x.CreateBucketRefresher(It.IsAny<BucketBase>()))
            .Returns((BucketBase b) => CreateRefresher(b, clock));
        using var bucket = new CouchbaseBucket("default", context, new Mock<IScopeFactory>().Object,
            new Mock<IRetryOrchestrator>().Object, new Mock<IVBucketKeyMapperFactory>().Object,
            new Mock<ILogger<CouchbaseBucket>>().Object, new Redactor(RedactionLevel.None),
            new Mock<IBootstrapperFactory>().Object, NoopRequestTracer.Instance,
            new Mock<IOperationConfigurator>().Object, new BestEffortRetryStrategy(), ServerMap(2, forward: false),
            refresherFactory.Object);
        var node = AddNode(bucket, "node-0", push: true);
        var fetches = new AsyncCounter();
        node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback(() => fetches.Increment())
            .ReturnsAsync((BucketConfig)null!);
        bucket.ConfigRefresher.Start();
        await clock.WaitForSleepAsync(PollInterval);

        clock.Advance(TimeSpan.FromSeconds(3));
        await bucket.ConfigUpdatedAsync(ServerMap(3, forward: true));

        // More than a second passed since the last poll, so the poll runs at once.
        await fetches.WaitForAsync(1);
        await clock.WaitForSleepAsync(BucketConfigRefresher.RebalancePollInterval);
    }

    [Theory]
    [InlineData(500, 1000)]
    [InlineData(2500, 2500)]
    [InlineData(10000, 5000)]
    public async Task Fetch_TimesOutAfterThePollIntervalBetweenOneAndFiveSeconds(int pollIntervalMs, int timeoutMs)
    {
        var bucket = CreateBucketMock(initialConfig: Version(2),
            configure: options => options.ConfigPollInterval = TimeSpan.FromMilliseconds(pollIntervalMs));
        using var context = bucket.Context;
        var node = AddNode(bucket, "node-0", push: true);
        var timeout = new TaskCompletionSource<TimeSpan?>(TaskCreationOptions.RunContinuationsAsynchronously);
        node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback((ConfigVersion? _, TimeSpan? t, CancellationToken _) => timeout.TrySetResult(t))
            .ReturnsAsync((BucketConfig)null!);
        using var refresher = CreateRefresher(bucket, new SteppableClock());
        refresher.Start();

        refresher.RequestRefresh(new ConfigVersion(1, 3));

        Assert.Equal(TimeSpan.FromMilliseconds(timeoutMs), await timeout.Task);
    }

    [Fact]
    public async Task Fetch_WhenKvServesNoConfig_SwitchesTheBucketToHttpStreaming()
    {
        var streaming = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bucket = CreateBucketMock(initialConfig: Version(2),
            onStartHttpStreaming: name => streaming.TrySetResult(name));
        using var context = bucket.Context;
        var node = AddNode(bucket, "node-0", push: true);
        node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DocumentNotFoundException());
        var logger = new LoopExitLogger();
        using var refresher = CreateRefresher(bucket, new SteppableClock(), logger);
        refresher.Start();

        refresher.RequestRefresh(new ConfigVersion(1, 3));

        Assert.Equal("default", await streaming.Task);
        await logger.Exited;
    }

    /// <summary>
    /// Steps the clock through the first <paramref name="retries"/> retries of a pushed version.
    /// </summary>
    private static async Task StepThroughRetriesAsync(SteppableClock clock, int retries)
    {
        for (var retry = 0; retry < retries; retry++)
        {
            var delay = ConfigRefresher.GetNullConfigRetryDelay(retry);
            await clock.WaitForSleepAsync(delay);
            clock.Advance(delay);
        }
    }

    /// <summary>
    /// A DNS SRV connection string. Each rebootstrap counts one lookup, finds no record and reaches no node.
    /// </summary>
    private static void UseDnsSrv(ClusterOptions options, AsyncCounter dnsLookups)
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
            [new HostEndpoint("node-0", 11210)], isDnsSrv: true, new Uri("couchbase://example.com"));
    }

    /// <summary>
    /// A bucket config with one node, whose vbucket map changes with the rev.
    /// </summary>
    private static BucketConfig ServerMap(ulong rev, bool forward)
    {
        var config = Version(rev);
        short[][] map = [[(short)(rev % 2)]];
        config.VBucketServerMap = new VBucketServerMapDto
        {
            ServerList = ["node-0:11210"],
            VBucketMap = map,
            VBucketMapForward = forward ? map : []
        };
        return config;
    }

    private static BucketConfig Version(ulong rev)
    {
        var config = new BucketConfig { Name = "default", RevEpoch = 1, Rev = rev };
        config.OnDeserialized();
        return config;
    }

    private static Mock<IClusterNode> AddNode(BucketBase bucket, string host, bool push)
    {
        var node = new Mock<IClusterNode>();
        node.SetupGet(x => x.EndPoint).Returns(new HostEndpointWithPort(host, 11210));
        node.SetupGet(x => x.HasKv).Returns(true);
        node.SetupGet(x => x.IsDead).Returns(false);
        node.SetupGet(x => x.KeyEndPoints).Returns(new List<HostEndpointWithPort>());
        node.SetupGet(x => x.ServerFeatures).Returns(push
            ? new ServerFeatureSet([ServerFeatures.ClustermapChangeNotificationBrief])
            : ServerFeatureSet.Empty);
        bucket.Nodes.Add(node.Object);
        return node;
    }

    private BucketConfigRefresher CreateRefresher(BucketBase bucket, TimeProvider clock, ILogger? logger = null,
        [CallerMemberName] string caller = "")
    {
        // The config handler mock the bucket context publishes to.
        var configHandler = bucket.Context.ServiceProvider.GetRequiredService<IConfigHandler>();
        return new BucketConfigRefresher(bucket, configHandler, logger ?? new TestOutputLogger(outputHelper, caller),
            new Redactor(RedactionLevel.None), clock);
    }

    /// <summary>
    /// Records warnings and completes <see cref="Exited"/> when the refresher loop ends.
    /// </summary>
    private sealed class LoopExitLogger : ILogger
    {
        private readonly TaskCompletionSource<bool> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<string> _warnings = new();

        public Task Exited => _exited.Task;

        public IReadOnlyList<string> Warnings
        {
            get { lock (_warnings) return _warnings.ToArray(); }
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (logLevel >= LogLevel.Warning)
            {
                lock (_warnings) _warnings.Add(message);
            }

            if (message == "The config refresher of default stopped")
            {
                _exited.TrySetResult(true);
            }
        }
    }

    private BucketBase CreateBucketMock(
        string bucketName = "default",
        BucketConfig? initialConfig = null,
        Action<BucketConfig?>? onPublish = null,
        Action<string?>? onStartHttpStreaming = null,
        Action<ClusterOptions>? configure = null)
    {
        onPublish ??= _ => { };
        onStartHttpStreaming ??= _ => { };
        initialConfig ??= new();
        Action doNothing = () => { };
        var mockCluster = new Mock<ICluster>(MockBehavior.Strict);
        var mockConfigHandler = new Mock<IConfigHandler>(MockBehavior.Strict);
        mockConfigHandler.Setup(ch => ch.Publish(It.IsAny<BucketConfig>())).Callback(onPublish);
        mockConfigHandler.Setup(ch => ch.Subscribe(It.IsAny<IConfigUpdateEventSink>())).Callback(doNothing);
        mockConfigHandler.Setup(ch => ch.StartHttpStreaming(It.IsAny<string?>())).Callback(onStartHttpStreaming);
        mockConfigHandler.Setup(ch => ch.Dispose()); // the tests dispose the ClusterContext
        var clusterOptions = new ClusterOptions().WithLogging(new TestOutputLoggerFactory(outputHelper)).WithPasswordAuthentication("username", "password");
        clusterOptions.ConfigPollInterval = PollInterval;
        configure?.Invoke(clusterOptions);
        clusterOptions.AddClusterService(mockConfigHandler.Object);
        var mock = new Mock<BucketBase>(
            bucketName,
            new ClusterContext(mockCluster.Object, new CancellationTokenSource(), clusterOptions),
            new Mock<IScopeFactory>().Object,
            new Mock<IRetryOrchestrator>().Object,
            new TestOutputLogger(outputHelper, nameof(BucketConfigRefresherTests)),
            new Redactor(RedactionLevel.None),
            new Mock<IBootstrapperFactory>().Object,
            NoopRequestTracer.Instance,
            new Mock<IOperationConfigurator>().Object,
            new BestEffortRetryStrategy(),
            initialConfig);

        mock.SetupGet(it => it.Name).Returns(bucketName);
        mock.Setup(it => it.ConfigUpdatedAsync(It.IsAny<BucketConfig>()))
            .Returns((BucketConfig bc) =>
            {
                outputHelper.WriteLine("Config Published: bucket={0}, version={1}", bucketName, bc.ConfigVersion);
                return Task.CompletedTask;
            });
        return mock.Object;
    }
}
