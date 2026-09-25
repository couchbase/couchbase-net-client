using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
    // Initialize starts the real reporter loop, so nodes use loopback ports where nothing listens and connects fail fast.
    private const int PortA = 1;
    private const int PortB = 2;
    private static readonly Uri NodeA = new("ws://127.0.0.1:1/_appTelemetry");
    private static readonly Uri NodeB = new("ws://127.0.0.1:2/_appTelemetry");
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    #region Collector

    [Fact]
    public void Initialize_Without_Paths_Pauses_And_Records_Nothing()
    {
        using var collector = CreateCollector();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, false)));

        collector.Initialize();

        Assert.True(collector.IsPaused);
        Assert.Empty(collector.WebSocketClientHandler!.Remotes);
        TrackOperation(collector);
        Assert.Empty(collector.MetricSets);
    }

    [Fact]
    public void Newer_Config_With_Paths_Pushes_Remotes_And_Resumes()
    {
        using var collector = CreateCollector();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, false)));
        collector.Initialize();

        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 2, (PortA, true)));

        Assert.False(collector.IsPaused);
        AssertRemotes(collector, NodeA);
        TrackOperation(collector);
        Assert.NotEmpty(collector.MetricSets);
    }

    [Fact]
    public void Older_Or_Equal_Config_Is_Ignored_Before_Computing_Uris()
    {
        var loggerFactory = new CountingLoggerFactory();
        using var collector = CreateCollector(options => options.WithLogging(loggerFactory));
        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 5, (PortA, true)));

        // Building URIs for this config throws, so it proves the version check runs first.
        var sameRev = CreatePoisonConfig(BucketConfig.GlobalBucketName, 5);
        var olderRev = CreatePoisonConfig(BucketConfig.GlobalBucketName, 4);
        Assert.ThrowsAny<Exception>(() => sameRev.GetAppTelemetryUris(false, sameRev.NetworkResolution));

        // The collector logs the errors it catches, so no log means no URIs were built.
        collector.OnConfigUpdated(sameRev);
        collector.OnConfigUpdated(olderRev);
        Assert.Equal(0, loggerFactory.Count("could not process config"));
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 5, (PortB, true)));

        AssertRemotes(collector, NodeA);
    }

    [Fact]
    public void IgnoreRev_Config_Is_Accepted_With_Same_Version()
    {
        using var collector = CreateCollector();
        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketName, 5, (PortA, true)));

        var config = CreateConfig(BucketName, 5, (PortB, true));
        config.IgnoreRev = true;
        collector.OnConfigUpdated(config);

        AssertRemotes(collector, NodeB);
    }

    [Fact]
    public void Remotes_Are_The_Union_Of_Global_And_Bucket_Configs()
    {
        using var collector = CreateCollector();
        collector.Initialize();

        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, true)));
        collector.OnConfigUpdated(CreateConfig(BucketName, 1, (PortA, true), (PortB, true)));

        AssertRemotes(collector, NodeA, NodeB);
    }

    [Fact]
    public void Path_Disappears_Pauses_And_Discards_Metrics()
    {
        using var collector = CreateCollector();
        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, true)));
        TrackOperation(collector);
        Assert.NotEmpty(collector.MetricSets);

        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 2, (PortA, false)));

        Assert.True(collector.IsPaused);
        Assert.Empty(collector.MetricSets);
        Assert.Empty(collector.WebSocketClientHandler!.Remotes);
        Assert.False(collector.TryExportMetricsAndReset(out _));
    }

    [Fact]
    public void Removed_Bucket_Leaves_The_Remote_Set()
    {
        using var collector = CreateCollector();
        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, true)));
        collector.OnConfigUpdated(CreateConfig(BucketName, 1, (PortB, true)));
        AssertRemotes(collector, NodeA, NodeB);

        collector.OnConfigRemoved(BucketName);

        AssertRemotes(collector, NodeA);
    }

    [Fact]
    public void Configs_Before_Initialize_Are_Kept()
    {
        using var collector = CreateCollector();

        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, false)));
        collector.OnConfigUpdated(CreateConfig(BucketName, 1, (PortB, true)));
        collector.Initialize();

        Assert.False(collector.IsPaused);
        AssertRemotes(collector, NodeB);
    }

    [Fact]
    public void Explicit_Endpoint_Ignores_Configs_And_Never_Pauses()
    {
        var endpoint = new Uri("ws://127.0.0.1:3/collect");
        using var collector = CreateCollector(options => options.WithAppTelemetryEndpoint(endpoint));

        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, true)));
        collector.OnConfigUpdated(CreateConfig(BucketName, 1, (PortB, false)));

        Assert.False(collector.IsPaused);
        AssertRemotes(collector, endpoint);
        TrackOperation(collector);
        Assert.NotEmpty(collector.MetricSets);
    }

    [Fact]
    public void Disabled_In_Options_Ignores_Configs()
    {
        using var collector = CreateCollector(options => options.WithAppTelemetryEnabled(false));

        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, true)));

        Assert.Null(collector.WebSocketClientHandler);
    }

    [Fact]
    public void Initialize_Twice_Keeps_One_Handler()
    {
        using var collector = CreateCollector();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, true)));

        collector.Initialize();
        var handler = collector.WebSocketClientHandler;
        collector.Initialize();

        Assert.Same(handler, collector.WebSocketClientHandler);
        AssertRemotes(collector, NodeA);
    }

    [Fact]
    public void Uses_Tls_Scheme_From_Options()
    {
        using var collector = CreateCollector(options => options.EnableTls = true);
        collector.Initialize();

        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, true)));

        AssertRemotes(collector, new Uri("wss://127.0.0.1:101/_appTelemetry"));
    }

    #endregion

    #region WebSocketClientHandler loop

    [Fact]
    public async Task Handler_Without_Remotes_Does_Not_Connect_Or_Spin()
    {
        var loggerFactory = new CountingLoggerFactory();
        var recorder = new SessionRecorder();
        using var collector = CreateCollector(options => options.WithLogging(loggerFactory));
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
        using var collector = CreateCollector();
        using var handler = new WebSocketClientHandler(collector, recorder.RunAsync);
        using var cts = new CancellationTokenSource();
        var loop = handler.StartAsync(cts.Token);
        await Task.Delay(100);

        handler.UpdateRemotes(new[] { NodeA });

        Assert.Equal(NodeA, (await recorder.NextAsync()).Remote);

        cts.Cancel();
        await AssertCompletesAsync(loop);
    }

    [Fact]
    public async Task Handler_Tries_Each_Remote_Once_Before_Backing_Off()
    {
        var recorder = new SessionRecorder(fail: true);
        using var collector = CreateCollector();
        using var handler = new WebSocketClientHandler(collector, recorder.RunAsync);
        using var cts = new CancellationTokenSource();
        handler.UpdateRemotes(new[] { NodeA, NodeB });
        var loop = handler.StartAsync(cts.Token);

        var sessions = new List<(Uri Remote, TimeSpan At)>();
        for (var i = 0; i < 5; i++)
        {
            sessions.Add(await recorder.NextAsync());
        }

        // The first pass tries both remotes, then the order repeats.
        Assert.Equal(new[] { NodeA, NodeB }, sessions.Take(2).Select(s => s.Remote).OrderBy(u => u.Port));
        Assert.Equal(sessions[0].Remote, sessions[2].Remote);
        // The second pass waits 100ms per attempt and the third pass 200ms.
        Assert.True(sessions[2].At - sessions[1].At >= TimeSpan.FromMilliseconds(80));
        Assert.True(sessions[4].At - sessions[3].At >= TimeSpan.FromMilliseconds(180));

        cts.Cancel();
        await AssertCompletesAsync(loop);
    }

    [Fact]
    public async Task Handler_Retries_Without_Delay_When_Remotes_Change()
    {
        var recorder = new SessionRecorder(fail: true);
        using var collector = CreateCollector();
        using var handler = new WebSocketClientHandler(collector, recorder.RunAsync);
        using var cts = new CancellationTokenSource();
        handler.UpdateRemotes(new[] { NodeA });
        var loop = handler.StartAsync(cts.Token);

        // After 5 failed attempts the next delay is 1.6s.
        for (var i = 0; i < 5; i++)
        {
            await recorder.NextAsync();
        }

        var changedAt = recorder.Elapsed;
        handler.UpdateRemotes(new[] { NodeB });

        var session = await recorder.NextAsync();
        Assert.Equal(NodeB, session.Remote);
        Assert.True(session.At - changedAt < TimeSpan.FromMilliseconds(800));

        cts.Cancel();
        await AssertCompletesAsync(loop);
    }

    [Fact]
    public async Task Handler_Loop_Ends_On_Cancel_And_Survives_Dispose()
    {
        var recorder = new SessionRecorder();
        using var collector = CreateCollector();
        var handler = new WebSocketClientHandler(collector, recorder.RunAsync);
        using var cts = new CancellationTokenSource();
        handler.UpdateRemotes(new[] { NodeA });
        var loop = handler.StartAsync(cts.Token);
        await recorder.NextAsync();

        // Same order as AppTelemetryCollector.Dispose.
        cts.Cancel();
        handler.Dispose();

        await AssertCompletesAsync(loop);
        handler.UpdateRemotes(new[] { NodeB });
    }

    #endregion

    #region Helpers

    private static AppTelemetryCollector CreateCollector(Action<ClusterOptions> configure = null)
    {
        var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
        configure?.Invoke(options);
        var context = new ClusterContext(null, options);
        return new AppTelemetryCollector(context, new Mock<IRedactor>().Object, NullLogger<AppTelemetryCollector>.Instance);
    }

    private static void AssertRemotes(AppTelemetryCollector collector, params Uri[] expected) =>
        Assert.Equal(expected.OrderBy(u => u.OriginalString, StringComparer.Ordinal),
            collector.WebSocketClientHandler!.Remotes.OrderBy(u => u.OriginalString, StringComparer.Ordinal));

    private static void TrackOperation(AppTelemetryCollector collector) =>
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(5), "node1", null, "uuid1",
            AppTelemetryServiceType.Query, AppTelemetryCounterType.Total, AppTelemetryRequestType.Query);

    private static BucketConfig CreateConfig(string name, ulong rev, params (int Port, bool HasPath)[] nodes)
    {
        var nodesExt = nodes.Select(n => new Dictionary<string, object>
        {
            ["hostname"] = "127.0.0.1",
            ["services"] = new Dictionary<string, int> { ["mgmt"] = n.Port, ["mgmtSSL"] = n.Port + 100, ["kv"] = 11210 },
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

    /// <summary>
    /// Records each session. A session stays open until the loop is cancelled, or fails at once like a failed connect.
    /// </summary>
    private sealed class SessionRecorder(bool fail = false)
    {
        private readonly ConcurrentQueue<(Uri Remote, TimeSpan At)> _pending = new();
        private readonly SemaphoreSlim _started = new(0);
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public TimeSpan Elapsed => _clock.Elapsed;

        public async Task RunAsync(Uri remote, CancellationToken token)
        {
            Interlocked.Increment(ref _count);
            _pending.Enqueue((remote, _clock.Elapsed));
            _started.Release();
            if (fail) throw new InvalidOperationException("Connect failed.");
            await Task.Delay(Timeout.Infinite, token);
        }

        public async Task<(Uri Remote, TimeSpan At)> NextAsync()
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
