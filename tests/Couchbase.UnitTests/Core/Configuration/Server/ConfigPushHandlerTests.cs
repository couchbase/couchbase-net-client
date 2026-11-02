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
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Test.Common.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Configuration.Server;

public class ConfigPushHandlerTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task ConfigPushHandler_ServerVersionRegressed()
    {
        // server pushes (1,3), but returns (1,1)
        var initialBucketConfig = new BucketConfig() { RevEpoch = 1, Rev = 2 };
        initialBucketConfig.OnDeserialized(); // Required to properly initialize ConfigVersion
        var publishes = new AsyncCounter();
        var publishTcs = new TaskCompletionSource<BucketConfig?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Only recorded here, never asserted here: this runs on the handler's own loop, which swallows
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
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(getClusterMapResult))
            .Callback(() => fetches.Increment());
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockContext.Nodes.Add(mockNode.Object);
        mockBucket.Nodes.Add(mockNode.Object);
        mockContext.RegisterBucket(mockBucket);
        mockContext.Start();
        var logger = new TestOutputLogger(outputHelper, nameof(ConfigPushHandler_ServerVersionRegressed));
        var redactor = new Redactor(RedactionLevel.None);
        using var configPushHandler = new ConfigPushHandler(mockBucket, mockContext, logger, redactor, TimeProvider.System);
        var pushedVersion = new ConfigVersion(1, 3);
        configPushHandler.ProcessConfigPush(pushedVersion);

        // While the server returns an older version than was pushed, the handler must not publish it.
        // It re-arms itself and fetches again in that case, so a second fetch means the first lap ran
        // to completion — there is nothing to wait and see, and no deadline to lose on a busy runner.
        await fetches.WaitForAsync(2);
        Assert.Equal(0, publishes.Count);

        // Update the version of the config that is returned. This should result in a publish.
        getClusterMapResult.Rev = 3;
        getClusterMapResult.OnDeserialized();

        // No timeout: a handler which never publishes hangs the test, which is diagnosable, rather
        // than failing an assertion that says nothing about why.
        var publishedConfig = await publishTcs.Task;

        Assert.NotNull(publishedConfig);
        Assert.Equal(pushedVersion, publishedConfig!.ConfigVersion);
    }

    [Fact]
    public async Task ConfigPushHandler_ServerReturnsNullThenConfig_AsksAgainAndPublishes()
    {
        // The server pushes (1,3), but the node asked has nothing newer than (1,2) and returns null.
        // The same version is not pushed again, so the handler must ask again by itself.
        var initialBucketConfig = new BucketConfig() { RevEpoch = 1, Rev = 2 };
        initialBucketConfig.OnDeserialized();
        var publishTcs = new TaskCompletionSource<BucketConfig?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockBucket = CreateBucketMock(initialConfig: initialBucketConfig, onPublish: bc => publishTcs.TrySetResult(bc));
        ClusterContext mockContext = mockBucket.Context;
        var mockNode = new Mock<IClusterNode>();
        var newConfig = new BucketConfig() { RevEpoch = 1, Rev = 3 };
        newConfig.OnDeserialized();
        IReadOnlyCollection<HostEndpointWithPort> endpoints = new List<HostEndpointWithPort>();
        var fetches = new AsyncCounter();
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<BucketConfig>(fetches.Count < 3 ? null! : newConfig))
            .Callback(() => fetches.Increment());
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockContext.Nodes.Add(mockNode.Object);
        mockBucket.Nodes.Add(mockNode.Object);
        mockContext.RegisterBucket(mockBucket);
        mockContext.Start();
        var logger = new TestOutputLogger(outputHelper, nameof(ConfigPushHandler_ServerReturnsNullThenConfig_AsksAgainAndPublishes));
        var redactor = new Redactor(RedactionLevel.None);
        using var configPushHandler = new ConfigPushHandler(mockBucket, mockContext, logger, redactor, TimeProvider.System);

        configPushHandler.ProcessConfigPush(new ConfigVersion(1, 3));

        // No timeout: a handler which gives up after a null answer hangs the test, which is diagnosable.
        var publishedConfig = await publishTcs.Task;

        Assert.NotNull(publishedConfig);
        Assert.Equal(newConfig.ConfigVersion, publishedConfig!.ConfigVersion);
        // Three nulls, then the config.
        Assert.Equal(4, fetches.Count);
    }

    [Fact]
    public async Task ConfigPushHandler_FetchThrowsOnOneNode_PublishesFromAnotherNode()
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
        failingNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromException<BucketConfig>(new UnambiguousTimeoutException("The request has timed out.")))
            .Callback(() => failingFetches.Increment());

        var workingNode = new Mock<IClusterNode>();
        var workingFetches = new AsyncCounter();
        workingNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(workingFetches.Count < 1 ? null! : newConfig))
            .Callback(() => workingFetches.Increment());

        foreach (var mockNode in new[] { failingNode, workingNode })
        {
            mockNode.SetupGet(x => x.IsDead).Returns(false);
            mockNode.SetupGet(x => x.HasKv).Returns(true);
            mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
            mockContext.Nodes.Add(mockNode.Object);
            mockBucket.Nodes.Add(mockNode.Object);
        }

        mockContext.RegisterBucket(mockBucket);
        mockContext.Start();
        var logger = new TestOutputLogger(outputHelper, nameof(ConfigPushHandler_FetchThrowsOnOneNode_PublishesFromAnotherNode));
        var redactor = new Redactor(RedactionLevel.None);
        using var configPushHandler = new ConfigPushHandler(mockBucket, mockContext, logger, redactor, TimeProvider.System);

        configPushHandler.ProcessConfigPush(new ConfigVersion(1, 3));

        // No timeout. A handler that stops on the failed node hangs the test, which is easy to diagnose.
        var publishedConfig = await publishTcs.Task;

        Assert.NotNull(publishedConfig);
        Assert.Equal(newConfig.ConfigVersion, publishedConfig!.ConfigVersion);
        Assert.Equal(1, publishes.Count);
        Assert.Equal(2, workingFetches.Count);
        // Two when the second pass also asks the failing node first.
        Assert.InRange(failingFetches.Count, 1, 2);
    }

    [Fact]
    public async Task ConfigPushHandler_FetchThrowsThenReturnsConfig_AsksAgainAndPublishes()
    {
        // The server pushes (1,3), but the first fetch times out. The same version is not pushed again,
        // so the handler must ask again by itself.
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
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<CancellationToken>()))
            .Returns(() => fetches.Count < 1
                ? Task.FromException<BucketConfig>(new UnambiguousTimeoutException("The request has timed out."))
                : Task.FromResult(newConfig))
            .Callback(() => fetches.Increment());
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockContext.Nodes.Add(mockNode.Object);
        mockBucket.Nodes.Add(mockNode.Object);
        mockContext.RegisterBucket(mockBucket);
        mockContext.Start();
        var logger = new TestOutputLogger(outputHelper, nameof(ConfigPushHandler_FetchThrowsThenReturnsConfig_AsksAgainAndPublishes));
        var redactor = new Redactor(RedactionLevel.None);
        using var configPushHandler = new ConfigPushHandler(mockBucket, mockContext, logger, redactor, TimeProvider.System);

        configPushHandler.ProcessConfigPush(new ConfigVersion(1, 3));

        // No timeout. A handler that gives up after a failed fetch hangs the test, which is easy to diagnose.
        var publishedConfig = await publishTcs.Task;

        Assert.NotNull(publishedConfig);
        Assert.Equal(newConfig.ConfigVersion, publishedConfig!.ConfigVersion);
        // One failure, then the config.
        Assert.Equal(2, fetches.Count);
    }

    [Fact]
    public async Task ConfigPushHandler_HandlerCancelled_StopsFetching()
    {
        // The handler is disposed while a fetch waits on its token. It must not go on to the next node.
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
            mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<CancellationToken>()))
                .Returns(async (ConfigVersion? _, CancellationToken token) =>
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return newConfig;
                })
                .Callback(() => fetches.Increment());
            mockNode.SetupGet(x => x.IsDead).Returns(false);
            mockNode.SetupGet(x => x.HasKv).Returns(true);
            mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
            mockContext.Nodes.Add(mockNode.Object);
            mockBucket.Nodes.Add(mockNode.Object);
        }

        mockContext.RegisterBucket(mockBucket);
        mockContext.Start();
        var logger = new TestOutputLogger(outputHelper, nameof(ConfigPushHandler_HandlerCancelled_StopsFetching));
        var redactor = new Redactor(RedactionLevel.None);
        using var configPushHandler = new ConfigPushHandler(mockBucket, mockContext, logger, redactor, TimeProvider.System);

        configPushHandler.ProcessConfigPush(new ConfigVersion(1, 3));
        await fetches.WaitForAsync(1);

        configPushHandler.Dispose();

        // Give a handler which goes on to the next node time to do so.
        await Task.Delay(100);

        Assert.Equal(1, fetches.Count);
    }

    [Fact]
    public async Task ConfigPushHandler_DisposedDuringRetryDelay_LeavesLoopWithoutWarning()
    {
        // The node has nothing newer, so the handler waits for a retry on a clock that never moves.
        // Dispose must still end the loop, and shutdown is not a warning.
        var initialBucketConfig = new BucketConfig() { RevEpoch = 1, Rev = 2 };
        initialBucketConfig.OnDeserialized();
        var mockBucket = CreateBucketMock(initialConfig: initialBucketConfig);
        using ClusterContext mockContext = mockBucket.Context;
        IReadOnlyCollection<HostEndpointWithPort> endpoints = new List<HostEndpointWithPort>();
        var mockNode = new Mock<IClusterNode>();
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BucketConfig)null!);
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockContext.Nodes.Add(mockNode.Object);
        mockBucket.Nodes.Add(mockNode.Object);
        mockContext.RegisterBucket(mockBucket);
        mockContext.Start();
        var clock = new SteppableClock();
        var logger = new LoopExitLogger();
        var redactor = new Redactor(RedactionLevel.None);
        using var configPushHandler = new ConfigPushHandler(mockBucket, mockContext, logger, redactor, clock);

        configPushHandler.ProcessConfigPush(new ConfigVersion(1, 3));
        await clock.WaitForSleepAsync(ConfigPushHandler.InitialNullConfigRetryDelay);

        configPushHandler.Dispose();

        // No timeout. A loop that stays parked on the clock hangs the test, which is easy to diagnose.
        await logger.Exited;
        Assert.Empty(logger.Warnings);
    }

    /// <summary>
    /// Records warnings and completes <see cref="Exited"/> when the push handler loop ends.
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

            if (message == "Exited consumer loop")
            {
                _exited.TrySetResult(true);
            }
        }
    }

    [Fact]
    public void GetNullConfigRetryDelay_DoublesUpToTheMaximum()
    {
        Assert.Equal(ConfigPushHandler.InitialNullConfigRetryDelay, ConfigPushHandler.GetNullConfigRetryDelay(0));
        Assert.Equal(TimeSpan.FromTicks(ConfigPushHandler.InitialNullConfigRetryDelay.Ticks * 2), ConfigPushHandler.GetNullConfigRetryDelay(1));
        Assert.Equal(ConfigPushHandler.MaxNullConfigRetryDelay, ConfigPushHandler.GetNullConfigRetryDelay(ConfigPushHandler.MaxNullConfigRetries));
        Assert.Equal(ConfigPushHandler.MaxNullConfigRetryDelay, ConfigPushHandler.GetNullConfigRetryDelay(int.MaxValue));
    }

    [Fact]
    public void NullConfigRetryTracker_StopsAfterTheMaximumRetries()
    {
        var tracker = new ConfigPushHandler.NullConfigRetryTracker();
        var pushed = new ConfigVersion(1, 3);
        var effective = new ConfigVersion(1, 2);

        for (var i = 0; i < ConfigPushHandler.MaxNullConfigRetries; i++)
        {
            Assert.True(tracker.TryNextRetry(pushed, effective, out _));
        }

        Assert.False(tracker.TryNextRetry(pushed, effective, out _));
        Assert.Equal(ConfigPushHandler.MaxNullConfigRetries, tracker.Retries);
    }

    [Fact]
    public void NullConfigRetryTracker_ExternalPublishRestartsTheCount()
    {
        // Another handler publishes a config that moves this bucket's effective version, but it is still
        // below the pushed version. The retries then start again.
        var tracker = new ConfigPushHandler.NullConfigRetryTracker();
        var pushed = new ConfigVersion(1, 5);

        for (var i = 0; i < ConfigPushHandler.MaxNullConfigRetries; i++)
        {
            Assert.True(tracker.TryNextRetry(pushed, new ConfigVersion(1, 2), out _));
        }

        Assert.False(tracker.TryNextRetry(pushed, new ConfigVersion(1, 2), out _));
        Assert.True(tracker.TryNextRetry(pushed, new ConfigVersion(1, 3), out var delay));
        Assert.Equal(1, tracker.Retries);
        Assert.Equal(ConfigPushHandler.InitialNullConfigRetryDelay, delay);
    }

    [Fact]
    public void NullConfigRetryTracker_NewPushedVersionRestartsTheCount()
    {
        var tracker = new ConfigPushHandler.NullConfigRetryTracker();
        var effective = new ConfigVersion(1, 2);

        for (var i = 0; i < ConfigPushHandler.MaxNullConfigRetries; i++)
        {
            tracker.TryNextRetry(new ConfigVersion(1, 3), effective, out _);
        }

        Assert.True(tracker.TryNextRetry(new ConfigVersion(1, 4), effective, out _));
        Assert.Equal(1, tracker.Retries);
    }

    [Fact]
    public void NullConfigRetryTracker_ResetStartsTheCountAgain()
    {
        var tracker = new ConfigPushHandler.NullConfigRetryTracker();
        var pushed = new ConfigVersion(1, 3);
        ConfigVersion? effective = null;

        for (var i = 0; i < ConfigPushHandler.MaxNullConfigRetries; i++)
        {
            tracker.TryNextRetry(pushed, effective, out _);
        }

        tracker.Reset();

        Assert.True(tracker.TryNextRetry(pushed, effective, out _));
        Assert.Equal(1, tracker.Retries);
    }

    [Fact]
    public async Task ConfigPushHandler_WakeUpWithoutNewVersion_KeepsProcessingPushes()
    {
        // A wake-up can arrive with no newer version than the last one skipped, for example a retry release
        // that lands after the version was skipped. The handler must wait for the next push, and not stop.
        var initialBucketConfig = new BucketConfig() { RevEpoch = 1, Rev = 2 };
        initialBucketConfig.OnDeserialized();
        var publishTcs = new TaskCompletionSource<BucketConfig?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockBucket = CreateBucketMock(initialConfig: initialBucketConfig, onPublish: bc => publishTcs.TrySetResult(bc));
        ClusterContext mockContext = mockBucket.Context;
        var mockNode = new Mock<IClusterNode>();
        var newConfig = new BucketConfig() { RevEpoch = 1, Rev = 3 };
        newConfig.OnDeserialized();
        IReadOnlyCollection<HostEndpointWithPort> endpoints = new List<HostEndpointWithPort>();
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(newConfig));
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockContext.Nodes.Add(mockNode.Object);
        mockBucket.Nodes.Add(mockNode.Object);
        mockContext.RegisterBucket(mockBucket);
        mockContext.Start();
        var logger = new TestOutputLogger(outputHelper, nameof(ConfigPushHandler_WakeUpWithoutNewVersion_KeepsProcessingPushes));
        var redactor = new Redactor(RedactionLevel.None);
        using var configPushHandler = new ConfigPushHandler(mockBucket, mockContext, logger, redactor, TimeProvider.System);

        // Wake the handler with no version pushed, and wait until it has taken the wake-up. No timeout: the
        // loop takes it at once, and a handler which does not hangs the test, which is diagnosable.
        configPushHandler.TryReleaseNewVersionSemaphore();
        while (configPushHandler.PendingWakeups > 0)
        {
            await Task.Delay(1);
        }

        // Give a handler which stops on that wake-up time to do so before the push below.
        await Task.Delay(100);

        configPushHandler.ProcessConfigPush(new ConfigVersion(1, 3));

        var publishedConfig = await publishTcs.Task;

        Assert.NotNull(publishedConfig);
        Assert.Equal(newConfig.ConfigVersion, publishedConfig!.ConfigVersion);
    }

    [Fact]
    public async Task ConfigPushHandler_BasicAdvance()
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
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(),
                It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(getClusterMapResult));
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockContext.Nodes.Add(mockNode.Object);
        mockBucket.Nodes.Add(mockNode.Object);
        mockContext.RegisterBucket(mockBucket);
        mockContext.Start();
        var logger = new TestOutputLogger(outputHelper,
            nameof(ConfigPushHandler_BasicAdvance));
        var redactor = new Redactor(RedactionLevel.None);
        using var configPushHandler =
            new ConfigPushHandler(mockBucket, mockContext, logger,
                redactor, TimeProvider.System);
        configPushHandler.ProcessConfigPush(new ConfigVersion(1, 2));

        // Awaited with no timeout, and asserted out here rather than inside the publish callback: the
        // callback runs on the handler's loop, which swallows exceptions, so an assertion made there
        // was silently lost. This test previously also released the semaphore from a catch which
        // swallowed the assertions themselves, so it could not fail.
        var publishedConfig = await publishTcs.Task;

        Assert.NotNull(publishedConfig);
        Assert.Equal(expected: getClusterMapResult.ConfigVersion,
            actual: publishedConfig!.ConfigVersion);
    }

    private BucketBase CreateBucketMock(
        string bucketName = "default",
        BucketConfig? initialConfig = null,
        Action<BucketConfig?>? onPublish = null,
        [CallerMemberName] string caller = "CreateBucketMock")
    {
        onPublish ??= _ => { };
        initialConfig ??= new();
        Action doNothing = () => { };
        var mockCluster = new Mock<ICluster>(MockBehavior.Strict);
        var mockConfigHandler = new Mock<IConfigHandler>(MockBehavior.Strict);
        mockConfigHandler.Setup(ch => ch.Publish(It.IsAny<BucketConfig>())).Callback(onPublish);
        mockConfigHandler.Setup(ch => ch.Subscribe(It.IsAny<IConfigUpdateEventSink>())).Callback(doNothing);
        mockConfigHandler.Setup(ch => ch.Start(It.IsAny<bool>())).Callback(doNothing);
        mockConfigHandler.Setup(ch => ch.Dispose()); // the tests dispose the ClusterContext
        var clusterOptions = new ClusterOptions().WithLogging(new TestOutputLoggerFactory(outputHelper)).WithPasswordAuthentication("username", "password");
        clusterOptions.AddClusterService(mockConfigHandler.Object);
        var mock = new Mock<BucketBase>(
            bucketName,
            new ClusterContext(mockCluster.Object, new CancellationTokenSource(), clusterOptions),
            new Mock<IScopeFactory>().Object,
            new Mock<IRetryOrchestrator>().Object,
            new TestOutputLogger(outputHelper, nameof(ConfigPushHandlerTests)),
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
