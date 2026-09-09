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
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Test.Common.Utils;
using Couchbase.UnitTests.Helpers;
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
        ClusterContext mockContext = mockBucket.Context;
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
        var redactor = new TypedRedactor(RedactionLevel.None);
        using var configPushHandler = new ConfigPushHandler(mockBucket, mockContext, logger, redactor);
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
    public async Task ConfigPushHandler_BasicAdvance()
    {
        // server pushes (1,2), and returns (1,2)
        var initialBucketConfig = new BucketConfig()
            { RevEpoch = 1, Rev = 1 };
        var publishTcs = new TaskCompletionSource<BucketConfig?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockBucket = CreateBucketMock(
            initialConfig: initialBucketConfig, onPublish: bc => publishTcs.TrySetResult(bc));

        ClusterContext mockContext = mockBucket.Context;
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
        var redactor = new TypedRedactor(RedactionLevel.None);
        using var configPushHandler =
            new ConfigPushHandler(mockBucket, mockContext, logger,
                redactor);
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
        var clusterOptions = new ClusterOptions().WithLogging(new TestOutputLoggerFactory(outputHelper)).WithPasswordAuthentication("username", "password");
        clusterOptions.AddClusterService(mockConfigHandler.Object);
        var mock = new Mock<BucketBase>(
            bucketName,
            new ClusterContext(mockCluster.Object, new CancellationTokenSource(), clusterOptions),
            new Mock<IScopeFactory>().Object,
            new Mock<IRetryOrchestrator>().Object,
            new TestOutputLogger(outputHelper, nameof(ConfigPushHandlerTests)),
            new TypedRedactor(RedactionLevel.None),
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
