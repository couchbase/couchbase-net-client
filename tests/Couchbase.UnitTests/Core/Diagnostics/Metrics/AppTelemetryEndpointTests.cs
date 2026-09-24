using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.Diagnostics.Metrics;

/// <summary>
/// Tests how App Telemetry follows the endpoints advertised in cluster configs.
/// </summary>
[Collection("AppTelemetry")]
public class AppTelemetryEndpointTests
{
    private const string BucketName = "default";
    private static readonly Uri NodeA = new("ws://10.0.0.1:8091/_appTelemetry");
    private static readonly Uri NodeB = new("ws://10.0.0.2:8091/_appTelemetry");
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    #region Collector

    [Fact]
    public void Initialize_Without_Paths_Pauses_And_Records_Nothing()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder);
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, ("10.0.0.1", false)));

        collector.Initialize();

        Assert.True(collector.IsPaused);
        Assert.Empty(collector.WebSocketClientHandler!.Remotes);
        TrackOperation(collector);
        Assert.Empty(collector.MetricSets);
    }

    [Fact]
    public async Task Newer_Config_With_Paths_Pushes_Remotes_And_Resumes()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder);
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, ("10.0.0.1", false)));
        collector.Initialize();

        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 2, ("10.0.0.1", true)));

        Assert.False(collector.IsPaused);
        Assert.Equal(new[] { NodeA }, collector.WebSocketClientHandler!.Remotes);
        var session = await recorder.NextAsync();
        Assert.Equal(NodeA, session.Remote);

        TrackOperation(collector);
        Assert.NotEmpty(collector.MetricSets);
    }

    [Fact]
    public void Older_Or_Equal_Config_Is_Ignored_Before_Computing_Uris()
    {
        var loggerFactory = new CountingLoggerFactory();
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder, options => options.WithLogging(loggerFactory));
        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 5, ("10.0.0.1", true)));

        // Building URIs for this config throws, so it proves the version check runs first.
        var sameRev = CreatePoisonConfig(BucketConfig.GlobalBucketName, 5);
        var olderRev = CreatePoisonConfig(BucketConfig.GlobalBucketName, 4);
        Assert.ThrowsAny<Exception>(() => sameRev.GetAppTelemetryUris(false, sameRev.NetworkResolution));

        // The collector logs the errors it catches, so no log means no URIs were built.
        collector.OnConfigUpdated(sameRev);
        collector.OnConfigUpdated(olderRev);
        Assert.Equal(0, loggerFactory.Count("could not process config"));
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 5, ("10.0.0.2", true)));

        Assert.Equal(new[] { NodeA }, collector.WebSocketClientHandler!.Remotes);
    }

    [Fact]
    public void IgnoreRev_Config_Is_Accepted_With_Same_Version()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder);
        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketName, 5, ("10.0.0.1", true)));

        var config = CreateConfig(BucketName, 5, ("10.0.0.2", true));
        config.IgnoreRev = true;
        collector.OnConfigUpdated(config);

        Assert.Equal(new[] { NodeB }, collector.WebSocketClientHandler!.Remotes);
    }

    [Fact]
    public void Remotes_Are_The_Union_Of_Global_And_Bucket_Configs()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder);
        collector.Initialize();

        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, ("10.0.0.1", true)));
        collector.OnConfigUpdated(CreateConfig(BucketName, 1, ("10.0.0.1", true), ("10.0.0.2", true)));

        Assert.Equal(new[] { NodeA, NodeB }, collector.WebSocketClientHandler!.Remotes);
    }

    [Fact]
    public void Path_Disappears_Pauses_And_Discards_Metrics()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder);
        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, ("10.0.0.1", true)));
        TrackOperation(collector);
        Assert.NotEmpty(collector.MetricSets);

        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 2, ("10.0.0.1", false)));

        Assert.True(collector.IsPaused);
        Assert.Empty(collector.MetricSets);
        Assert.Empty(collector.WebSocketClientHandler!.Remotes);
        Assert.False(collector.TryExportMetricsAndReset(out _));
    }

    [Fact]
    public void Removed_Bucket_Leaves_The_Remote_Set()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder);
        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, ("10.0.0.1", true)));
        collector.OnConfigUpdated(CreateConfig(BucketName, 1, ("10.0.0.2", true)));
        Assert.Equal(new[] { NodeA, NodeB }, collector.WebSocketClientHandler!.Remotes);

        collector.OnConfigRemoved(BucketName);

        Assert.Equal(new[] { NodeA }, collector.WebSocketClientHandler!.Remotes);
    }

    [Fact]
    public void Configs_Before_Initialize_Are_Kept()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder);

        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, ("10.0.0.1", false)));
        collector.OnConfigUpdated(CreateConfig(BucketName, 1, ("10.0.0.2", true)));
        collector.Initialize();

        Assert.False(collector.IsPaused);
        Assert.Equal(new[] { NodeB }, collector.WebSocketClientHandler!.Remotes);
    }

    [Fact]
    public async Task Explicit_Endpoint_Ignores_Configs_And_Never_Pauses()
    {
        var endpoint = new Uri("ws://telemetry.example.com:9000/collect");
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder, options => options.WithAppTelemetryEndpoint(endpoint));

        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, ("10.0.0.1", true)));
        collector.OnConfigUpdated(CreateConfig(BucketName, 1, ("10.0.0.2", false)));

        Assert.False(collector.IsPaused);
        Assert.Equal(new[] { endpoint }, collector.WebSocketClientHandler!.Remotes);
        Assert.Equal(endpoint, (await recorder.NextAsync()).Remote);
        TrackOperation(collector);
        Assert.NotEmpty(collector.MetricSets);
    }

    [Fact]
    public void Disabled_In_Options_Ignores_Configs()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder, options => options.WithAppTelemetryEnabled(false));

        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, ("10.0.0.1", true)));

        Assert.Null(collector.WebSocketClientHandler);
    }

    [Fact]
    public async Task Initialize_Twice_Starts_One_Loop()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder);
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, ("10.0.0.1", true)));

        collector.Initialize();
        var handler = collector.WebSocketClientHandler;
        collector.Initialize();

        Assert.Same(handler, collector.WebSocketClientHandler);
        await recorder.NextAsync();
        await Task.Delay(200);
        Assert.Equal(1, recorder.Count);
    }

    [Fact]
    public void Uses_Tls_Scheme_From_Options()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder, options => options.EnableTls = true);
        collector.Initialize();

        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, ("10.0.0.1", true)));

        Assert.Equal(new[] { new Uri("wss://10.0.0.1:18091/_appTelemetry") }, collector.WebSocketClientHandler!.Remotes);
    }

    #endregion

    #region WebSocketClientHandler loop

    [Fact]
    public async Task Handler_Without_Remotes_Does_Not_Connect_Or_Spin()
    {
        var loggerFactory = new CountingLoggerFactory();
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder, options => options.WithLogging(loggerFactory));
        using var handler = new WebSocketClientHandler(collector, recorder.RunAsync);
        using var cts = new CancellationTokenSource();

        var loop = handler.StartAsync(cts.Token);
        await Task.Delay(300);

        Assert.Equal(0, recorder.Count);
        Assert.Equal(1, loggerFactory.Count("no remotes available"));
        Assert.False(loop.IsCompleted);

        cts.Cancel();
        await AssertCompletesAsync(loop);
    }

    [Fact]
    public async Task Handler_Connects_Promptly_When_A_Remote_Appears()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder);
        using var handler = new WebSocketClientHandler(collector, recorder.RunAsync);
        using var cts = new CancellationTokenSource();
        var loop = handler.StartAsync(cts.Token);
        await Task.Delay(100);

        handler.UpdateRemotes(new[] { NodeA });

        var session = await recorder.NextAsync();
        Assert.Equal(NodeA, session.Remote);
        Assert.Equal(NodeA, handler.SelectedRemote);

        cts.Cancel();
        await AssertCompletesAsync(loop);
        Assert.True(session.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Handler_Disconnects_And_Reselects_When_Remote_Is_Removed()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder);
        using var handler = new WebSocketClientHandler(collector, recorder.RunAsync);
        using var cts = new CancellationTokenSource();
        handler.UpdateRemotes(new[] { NodeA });
        var loop = handler.StartAsync(cts.Token);
        var first = await recorder.NextAsync();
        Assert.Equal(NodeA, first.Remote);

        handler.UpdateRemotes(new[] { NodeB });

        Assert.True(first.Token.IsCancellationRequested);
        var second = await recorder.NextAsync();
        Assert.Equal(NodeB, second.Remote);
        Assert.False(second.Token.IsCancellationRequested);
        Assert.False(loop.IsCompleted);

        cts.Cancel();
        await AssertCompletesAsync(loop);
    }

    [Fact]
    public async Task Handler_Keeps_Session_When_Other_Remotes_Change()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder);
        using var handler = new WebSocketClientHandler(collector, recorder.RunAsync);
        using var cts = new CancellationTokenSource();
        handler.UpdateRemotes(new[] { NodeA });
        var loop = handler.StartAsync(cts.Token);
        var first = await recorder.NextAsync();

        handler.UpdateRemotes(new[] { NodeA, NodeB });
        await Task.Delay(200);

        Assert.False(first.Token.IsCancellationRequested);
        Assert.Equal(1, recorder.Count);

        cts.Cancel();
        await AssertCompletesAsync(loop);
    }

    [Fact]
    public async Task Handler_Loop_Ends_On_Dispose()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(recorder);
        var handler = new WebSocketClientHandler(collector, recorder.RunAsync);
        handler.UpdateRemotes(new[] { NodeA });
        var loop = handler.StartAsync(CancellationToken.None);
        var session = await recorder.NextAsync();

        handler.Dispose();

        await AssertCompletesAsync(loop);
        Assert.True(session.Token.IsCancellationRequested);
    }

    #endregion

    #region Helpers

    private static AppTelemetryCollector CreateCollector(SessionRecorder recorder,
        Action<ClusterOptions> configure = null)
    {
        var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
        configure?.Invoke(options);
        var context = new ClusterContext(null, options);
        return new AppTelemetryCollector(context, new Mock<IRedactor>().Object, NullLogger<AppTelemetryCollector>.Instance)
        {
            SessionOverride = recorder.RunAsync
        };
    }

    private static void TrackOperation(AppTelemetryCollector collector) =>
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(5), "node1", null, "uuid1",
            AppTelemetryServiceType.Query, AppTelemetryCounterType.Total, AppTelemetryRequestType.Query);

    private static BucketConfig CreateConfig(string name, ulong rev, params (string Host, bool HasPath)[] nodes)
    {
        var nodesExt = nodes.Select(n => new Dictionary<string, object>
        {
            ["hostname"] = n.Host,
            ["services"] = new Dictionary<string, int> { ["mgmt"] = 8091, ["mgmtSSL"] = 18091, ["kv"] = 11210 },
            ["appTelemetryPath"] = n.HasPath ? "/_appTelemetry" : null
        });
        return Deserialize(new Dictionary<string, object> { ["rev"] = rev, ["name"] = name, ["nodesExt"] = nodesExt });
    }

    /// <summary>
    /// A config whose alternate address cannot be resolved, so building its URIs throws.
    /// </summary>
    private static BucketConfig CreatePoisonConfig(string name, ulong rev)
    {
        var nodeExt = new Dictionary<string, object>
        {
            ["hostname"] = "10.0.0.9",
            ["services"] = new Dictionary<string, int> { ["mgmt"] = 8091 },
            ["appTelemetryPath"] = "/_appTelemetry",
            ["alternateAddresses"] = new Dictionary<string, object>
            {
                ["other"] = new Dictionary<string, string> { ["hostname"] = "example.com" }
            }
        };
        return Deserialize(new Dictionary<string, object>
            { ["rev"] = rev, ["name"] = name, ["nodesExt"] = new[] { nodeExt } });
    }

    private static BucketConfig Deserialize(Dictionary<string, object> config) =>
        JsonSerializer.Deserialize(JsonSerializer.Serialize(config), InternalSerializationContext.Default.BucketConfig);

    private static async Task AssertCompletesAsync(Task task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
        Assert.Same(task, completed);
    }

    private sealed class SessionRecorder
    {
        private readonly ConcurrentQueue<(Uri Remote, CancellationToken Token)> _pending = new();
        private readonly SemaphoreSlim _started = new(0);
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public async Task RunAsync(Uri remote, CancellationToken token)
        {
            Interlocked.Increment(ref _count);
            _pending.Enqueue((remote, token));
            _started.Release();
            await Task.Delay(Timeout.Infinite, token);
        }

        public async Task<(Uri Remote, CancellationToken Token)> NextAsync()
        {
            Assert.True(await _started.WaitAsync(WaitTimeout), "No session was started.");
            Assert.True(_pending.TryDequeue(out var session));
            return session;
        }
    }

    private sealed class CountingLoggerFactory : ILoggerFactory
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public int Count(string text) => _messages.Count(m => m.Contains(text));

        public ILogger CreateLogger(string categoryName) => new CountingLogger(_messages);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class CountingLogger : ILogger
        {
            private readonly ConcurrentQueue<string> _messages;

            public CountingLogger(ConcurrentQueue<string> messages) => _messages = messages;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter) => _messages.Enqueue(formatter(state, exception));

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable BeginScope<TState>(TState state) => null;
        }
    }

    #endregion
}
