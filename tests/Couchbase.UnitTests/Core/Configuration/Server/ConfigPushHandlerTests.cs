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
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Configuration.Server;

public class ConfigPushHandlerTests
{
    private readonly ITestOutputHelper _outputHelper;

    public ConfigPushHandlerTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task ConfigPushHandler_ServerVersionRegressed()
    {
        await Task.Yield();
        // server pushes (1,3), but returns (1,1)
        var initialBucketConfig = new BucketConfig() { RevEpoch = 1, Rev = 2 };
        initialBucketConfig.OnDeserialized(); // Required to properly initialize ConfigVersion
        var versionPublished = new ConfigVersion(0, 0);
        var publishTcs = new TaskCompletionSource<ConfigVersion>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockBucket = CreateBucketMock(initialConfig: initialBucketConfig, onPublish: bc =>
        {
            Assert.NotNull(bc);
            versionPublished = bc!.ConfigVersion;
            // Signal when we publish the target version (1,3)
            if (bc.ConfigVersion == new ConfigVersion(1, 3))
            {
                publishTcs.TrySetResult(bc.ConfigVersion);
            }
        });
        ClusterContext mockContext = mockBucket.Context;
        var mockNode = new Mock<IClusterNode>();
        BucketConfig getClusterMapResult = new BucketConfig() { RevEpoch = 1, Rev = 1 };
        getClusterMapResult.OnDeserialized();
        IReadOnlyCollection<HostEndpointWithPort> endpoints = new List<HostEndpointWithPort>();
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(getClusterMapResult));
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockContext.Nodes.Add(mockNode.Object);
        mockBucket.Nodes.Add(mockNode.Object);
        mockContext.RegisterBucket(mockBucket);
        mockContext.Start();
        var logger = new TestOutputLogger(_outputHelper, nameof(ConfigPushHandler_ServerVersionRegressed));
        var redactor = new TypedRedactor(RedactionLevel.None);
        using var configPushHandler = new ConfigPushHandler(mockBucket, mockContext, logger, redactor);
        var pushedVersion = new ConfigVersion(1, 3);
        configPushHandler.ProcessConfigPush(pushedVersion);

        // while server is returning older version, do not publish
        // Give the handler some time to process the push (but it should NOT publish since fetched version is older)
        await Task.Delay(500);

        Assert.NotEqual(versionPublished, pushedVersion);

        // update the version of the config that is returned.  This should result in a publish.
        getClusterMapResult.Rev = 3;
        getClusterMapResult.OnDeserialized();

        // Wait for the publish to occur with a CI-friendly timeout
        var completedTask = await Task.WhenAny(publishTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completedTask == publishTcs.Task, $"Expected version {pushedVersion} to be published but got {versionPublished}");

        await Task.Delay(500);

        Assert.Equal(versionPublished, pushedVersion);
    }

    [Fact]
    public async Task ConfigPushHandler_BasicAdvance()
    {
        // server pushes (1,2), and returns (1,2)
        bool publishedAnything = false;
        var initialBucketConfig = new BucketConfig() { RevEpoch = 1, Rev = 1 };
        var versionPublished = new ConfigVersion(0, 0);
        var mockBucket = CreateBucketMock(initialConfig: initialBucketConfig, onPublish: bc =>
        {
            publishedAnything = true;
            Assert.NotNull(bc);
            versionPublished = bc!.ConfigVersion;
        });

        ClusterContext mockContext = mockBucket.Context;
        var mockNode = new Mock<IClusterNode>();
        BucketConfig getClusterMapResult = new BucketConfig() { RevEpoch = 1, Rev = 2 };
        getClusterMapResult.OnDeserialized();
        IReadOnlyCollection<HostEndpointWithPort> endpoints = new List<HostEndpointWithPort>();
        mockNode.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(getClusterMapResult));
        mockNode.SetupGet(x => x.IsDead).Returns(false);
        mockNode.SetupGet(x => x.HasKv).Returns(true);
        mockNode.SetupGet(x => x.KeyEndPoints).Returns(endpoints);
        mockContext.Nodes.Add(mockNode.Object);
        mockBucket.Nodes.Add(mockNode.Object);
        mockContext.RegisterBucket(mockBucket);
        mockContext.Start();
        var logger = new TestOutputLogger(_outputHelper, nameof(ConfigPushHandler_ServerVersionRegressed));
        var redactor = new TypedRedactor(RedactionLevel.None);
        using var configPushHandler = new ConfigPushHandler(mockBucket, mockContext, logger, redactor);
        configPushHandler.ProcessConfigPush(new ConfigVersion(1, 2));
        await Task.Delay(500);

        Assert.True(publishedAnything);
        Assert.Equal(expected: getClusterMapResult.ConfigVersion, actual: versionPublished);
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
        var clusterOptions = new ClusterOptions().WithLogging(new TestOutputLoggerFactory(_outputHelper));
        clusterOptions.AddClusterService(mockConfigHandler.Object);
        var mock = new Mock<BucketBase>(
            bucketName,
            new ClusterContext(mockCluster.Object, new CancellationTokenSource(), clusterOptions),
            new Mock<IScopeFactory>().Object,
            new Mock<IRetryOrchestrator>().Object,
            new TestOutputLogger(_outputHelper, nameof(ConfigPushHandlerTests)),
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
                _outputHelper.WriteLine("Config Published: bucket={}, version={}", bucketName, bc.ConfigVersion);
                return Task.CompletedTask;
            });
        return mock.Object;
    }

    internal class MockConfigUpdatedSink : IConfigUpdateEventSink
    {
        public Action<BucketConfig> ConfigUpdatedAction { get; set; } = _ => { };

        public Task ConfigUpdatedAsync(BucketConfig newConfig)
        {
            ConfigUpdatedAction(newConfig);
            return Task.CompletedTask;
        }

        public string Name => nameof(MockConfigUpdatedSink);
        public IEnumerable<IClusterNode> ClusterNodes { get; set; }
    }
}
