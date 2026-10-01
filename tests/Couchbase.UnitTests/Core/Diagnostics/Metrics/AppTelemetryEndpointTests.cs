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
using Microsoft.Extensions.Time.Testing;
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
    public void Bucket_Configs_Replace_The_Global_Config_While_A_Bucket_Is_Open()
    {
        using var collector = CreateCollector();
        collector.Initialize();

        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, true), (PortB, true)));
        AssertRemotes(collector, NodeA, NodeB);

        collector.OnConfigUpdated(CreateConfig(BucketName, 1, (PortA, true), (PortB, false)));
        AssertRemotes(collector, NodeA);
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
    public void Bucket_Path_Disappears_Pauses_Although_Global_Config_Is_Stale()
    {
        using var collector = CreateCollector();
        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, true)));
        collector.OnConfigUpdated(CreateConfig(BucketName, 1, (PortA, true)));

        collector.OnConfigUpdated(CreateConfig(BucketName, 2, (PortA, false)));

        Assert.True(collector.IsPaused);
        Assert.Empty(collector.WebSocketClientHandler!.Remotes);
    }

    [Fact]
    public void Removed_Bucket_Leaves_The_Remote_Set()
    {
        using var collector = CreateCollector();
        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, true)));
        collector.OnConfigUpdated(CreateConfig(BucketName, 1, (PortA, true), (PortB, true)));
        AssertRemotes(collector, NodeA, NodeB);

        collector.OnConfigRemoved(BucketName);

        AssertRemotes(collector, NodeA);
    }

    [Fact]
    public void Global_Config_Is_The_Source_Again_After_The_Last_Bucket_Is_Removed()
    {
        const string otherBucket = "other";
        using var collector = CreateCollector();
        collector.Initialize();
        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, true)));
        collector.OnConfigUpdated(CreateConfig(BucketName, 1, (PortA, true), (PortB, true)));
        collector.OnConfigUpdated(CreateConfig(otherBucket, 1, (PortA, true), (PortB, true)));

        collector.OnConfigRemoved(BucketName);
        AssertRemotes(collector, NodeA, NodeB);

        collector.OnConfigRemoved(otherBucket);
        AssertRemotes(collector, NodeA);
    }

    [Fact]
    public void Configs_Before_Initialize_Are_Kept()
    {
        using var collector = CreateCollector();

        collector.OnConfigUpdated(CreateConfig(BucketConfig.GlobalBucketName, 1, (PortA, true), (PortB, false)));
        collector.Initialize();

        Assert.False(collector.IsPaused);
        AssertRemotes(collector, NodeA);
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
        var time = new TimerTrackingTimeProvider();
        var recorder = new SessionRecorder(time);
        using var collector = CreateCollector(options => options.WithLogging(loggerFactory));
        using var handler = new WebSocketClientHandler(collector, recorder.RunAsync, time);
        using var cts = new CancellationTokenSource();

        // The loop runs synchronously until it parks on the wake signal.
        var loop = handler.StartAsync(cts.Token);
        time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(0, recorder.Count);
        Assert.Equal(0, time.TimerCount);
        Assert.Equal(1, loggerFactory.Count("no remotes available"));
        Assert.False(loop.IsCompleted);

        cts.Cancel();
        await AssertCompletesAsync(loop);
    }

    [Fact]
    public async Task Handler_Connects_Promptly_When_A_Remote_Appears()
    {
        var time = new TimerTrackingTimeProvider();
        var recorder = new SessionRecorder(time);
        using var collector = CreateCollector();
        using var handler = new WebSocketClientHandler(collector, recorder.RunAsync, time);
        using var cts = new CancellationTokenSource();
        var loop = handler.StartAsync(cts.Token);

        handler.UpdateRemotes(new[] { NodeA });

        Assert.Equal(NodeA, (await recorder.NextAsync()).Remote);
        Assert.Equal(0, time.TimerCount);

        cts.Cancel();
        await AssertCompletesAsync(loop);
    }

    [Fact]
    public async Task Handler_Tries_Each_Remote_Once_Before_Backing_Off()
    {
        var time = new TimerTrackingTimeProvider();
        var recorder = new SessionRecorder(time, fail: true);
        using var collector = CreateCollector();
        using var handler = new WebSocketClientHandler(collector, recorder.RunAsync, time);
        using var cts = new CancellationTokenSource();
        handler.UpdateRemotes(new[] { NodeA, NodeB });
        var loop = handler.StartAsync(cts.Token);

        // The first pass tries both remotes without delay.
        var start = time.GetUtcNow();
        var sessions = new List<(Uri Remote, DateTimeOffset At)> { await recorder.NextAsync(), await recorder.NextAsync() };
        Assert.Equal(new[] { NodeA, NodeB }, sessions.Select(s => s.Remote).OrderBy(u => u.Port));
        Assert.All(sessions, s => Assert.Equal(start, s.At));

        // The second pass waits 100ms per attempt and the third pass 200ms. The order repeats.
        sessions.Add(await AssertBacksOffAsync(time, recorder, TimeSpan.FromMilliseconds(100)));
        sessions.Add(await AssertBacksOffAsync(time, recorder, TimeSpan.FromMilliseconds(100)));
        sessions.Add(await AssertBacksOffAsync(time, recorder, TimeSpan.FromMilliseconds(200)));
        Assert.Equal(sessions.Take(3).Select(s => s.Remote), sessions.Skip(2).Select(s => s.Remote));

        cts.Cancel();
        await AssertCompletesAsync(loop);
    }

    [Fact]
    public async Task Handler_Retries_Without_Delay_When_Remotes_Change()
    {
        var time = new TimerTrackingTimeProvider();
        var recorder = new SessionRecorder(time, fail: true);
        using var collector = CreateCollector();
        using var handler = new WebSocketClientHandler(collector, recorder.RunAsync, time);
        using var cts = new CancellationTokenSource();
        handler.UpdateRemotes(new[] { NodeA });
        var loop = handler.StartAsync(cts.Token);

        await recorder.NextAsync();
        foreach (var ms in new[] { 100, 200, 400, 800 })
        {
            await AssertBacksOffAsync(time, recorder, TimeSpan.FromMilliseconds(ms));
        }

        // After 5 failed attempts the next delay is 1.6s.
        Assert.Equal(TimeSpan.FromMilliseconds(1600), await time.NextTimerAsync());

        handler.UpdateRemotes(new[] { NodeB });

        Assert.Equal(NodeB, (await recorder.NextAsync()).Remote);

        cts.Cancel();
        await AssertCompletesAsync(loop);
    }

    [Fact]
    public async Task Handler_Loop_Ends_On_Cancel_And_Survives_Dispose()
    {
        var time = new TimerTrackingTimeProvider();
        var recorder = new SessionRecorder(time);
        using var collector = CreateCollector();
        var handler = new WebSocketClientHandler(collector, recorder.RunAsync, time);
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
        return new AppTelemetryCollector(context, new Redactor(RedactionLevel.None), NullLogger<AppTelemetryCollector>.Instance);
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
        // Surface a faulted loop.
        await task;
    }

    /// <summary>
    /// Waits for the loop to arm a timer of <paramref name="delay"/>, checks that no session starts
    /// before it is due, then returns the session that starts when it fires.
    /// </summary>
    private static async Task<(Uri Remote, DateTimeOffset At)> AssertBacksOffAsync(
        TimerTrackingTimeProvider time, SessionRecorder recorder, TimeSpan delay)
    {
        Assert.Equal(delay, await time.NextTimerAsync());
        var count = recorder.Count;

        time.Advance(delay - TimeSpan.FromMilliseconds(1));
        Assert.Equal(count, recorder.Count);

        time.Advance(TimeSpan.FromMilliseconds(1));
        var session = await recorder.NextAsync();
        Assert.Equal(time.GetUtcNow(), session.At);
        return session;
    }

    /// <summary>
    /// Records each session at the fake time it starts. A session stays open until the loop is cancelled,
    /// or fails at once like a failed connect.
    /// </summary>
    private sealed class SessionRecorder(TimeProvider time, bool fail = false)
    {
        private readonly ConcurrentQueue<(Uri Remote, DateTimeOffset At)> _pending = new();
        private readonly SemaphoreSlim _started = new(0);
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public async Task RunAsync(Uri remote, CancellationToken token)
        {
            Interlocked.Increment(ref _count);
            _pending.Enqueue((remote, time.GetUtcNow()));
            _started.Release();
            if (fail) throw new InvalidOperationException("Connect failed.");
            await Task.Delay(Timeout.Infinite, token);
        }

        public async Task<(Uri Remote, DateTimeOffset At)> NextAsync()
        {
            Assert.True(await _started.WaitAsync(WaitTimeout), "No session was started.");
            Assert.True(_pending.TryDequeue(out var session));
            return session;
        }
    }

    /// <summary>
    /// Reports the due time of each timer, so a test advances the clock only after the loop arms it.
    /// </summary>
    private sealed class TimerTrackingTimeProvider : FakeTimeProvider
    {
        private readonly ConcurrentQueue<TimeSpan> _dueTimes = new();
        private readonly SemaphoreSlim _created = new(0);
        private int _timerCount;

        public int TimerCount => Volatile.Read(ref _timerCount);

        public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            Interlocked.Increment(ref _timerCount);
            _dueTimes.Enqueue(dueTime);
            _created.Release();
            return timer;
        }

        public async Task<TimeSpan> NextTimerAsync()
        {
            Assert.True(await _created.WaitAsync(WaitTimeout), "No timer was created.");
            Assert.True(_dueTimes.TryDequeue(out var dueTime));
            return dueTime;
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
