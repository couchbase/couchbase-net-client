using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Configuration.Server;

/// <summary>
/// Keeps one config up to date over KV, like the config refreshers of the Java SDK. One background loop fetches the
/// config from a random KV node when a poll is due, and at once when the server pushes a newer version. Each fetch
/// sends the version the client has, so a node returns a config only when it has a newer one.
/// </summary>
internal abstract partial class ConfigRefresher : IDisposable
{
    // A node can be pushed a config version before the node asked for the config has it, and the server
    // then answers that it has nothing newer. The same version is not pushed again, so the loop asks again.
    internal const int MaxNullConfigRetries = 20;
    internal static readonly TimeSpan InitialNullConfigRetryDelay = TimeSpan.FromMilliseconds(10);
    internal static readonly TimeSpan MaxNullConfigRetryDelay = TimeSpan.FromMilliseconds(500);

    internal static readonly TimeSpan MinFetchTimeout = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan MaxFetchTimeout = TimeSpan.FromSeconds(5);

    // Stands for no due time.
    private static readonly TimeSpan Never = TimeSpan.MaxValue;

    // Delay rejects a longer sleep on .NET Framework. A loop with nothing due wakes up and sleeps again.
    internal static readonly TimeSpan MaxSleep = TimeSpan.FromMilliseconds(int.MaxValue);

    // The wait after an unexpected error when the poll interval is zero or less. It is the default poll interval.
    internal static readonly TimeSpan DefaultErrorDelay = TimeSpan.FromSeconds(2.5);

    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _disposeSource = new();
    private readonly object _lock = new();

    // Completed to wake the loop. The loop replaces it before it reads the requests, so it misses no request.
    private TaskCompletionSource<bool> _wakeUp = NewWakeUp();

    // Guarded by _lock.
    private ConfigVersion? _latestPushedVersion;
    private bool _pushedVersionsForgotten;
    private bool _started;
    private bool _disposed;

    // Only the loop reads and writes these.
    private long _lastPoll;
    private NullConfigRetryTracker _retries;
    private ConfigVersion _handledPushedVersion;
    private ConfigVersion? _retryVersion;
    private long _retryFrom;
    private TimeSpan _retryDelay;

    protected ConfigRefresher(ClusterContext context, IConfigHandler configHandler, ILogger logger, Redactor redactor,
        TimeProvider timeProvider)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        ConfigHandler = configHandler ?? throw new ArgumentNullException(nameof(configHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    protected ClusterContext Context { get; }

    protected IConfigHandler ConfigHandler { get; }

    protected Redactor Redactor { get; }

    /// <summary>
    /// The name of the config in logs.
    /// </summary>
    protected abstract string Name { get; }

    /// <summary>
    /// The nodes that serve the config. A fetch asks the live KV nodes among them that have had a connection.
    /// </summary>
    protected abstract IEnumerable<IClusterNode> Nodes { get; }

    /// <summary>
    /// The version of the config the client has, or null when it has none.
    /// </summary>
    protected abstract ConfigVersion? CurrentVersion { get; }

    /// <summary>
    /// The time from the last answer to the next poll.
    /// </summary>
    protected virtual TimeSpan PollInterval => Context.ClusterOptions.ConfigPollInterval;

    /// <summary>
    /// Sends a fetched config that is newer than <see cref="CurrentVersion"/> to the subscribers.
    /// </summary>
    protected virtual void Publish(BucketConfig config) => ConfigHandler.Publish(config);

    /// <summary>
    /// Gets the config over HTTP streaming from now on, because KV does not serve it.
    /// </summary>
    protected abstract void StartHttpStreaming();

    /// <summary>
    /// Tells the context that every node failed to return the config.
    /// </summary>
    protected virtual void SignalRefreshFailed() => Context.SignalConfigRefreshFailed();

    /// <summary>
    /// The timeout of a config fetch. It is the poll interval, kept between 1 and 5 seconds like the Java SDK.
    /// </summary>
    internal static TimeSpan GetFetchTimeout(TimeSpan pollInterval) =>
        pollInterval < MinFetchTimeout ? MinFetchTimeout
        : pollInterval > MaxFetchTimeout ? MaxFetchTimeout
        : pollInterval;

    /// <summary>
    /// Starts the loop. The first poll is due one poll interval later.
    /// </summary>
    public void Start()
    {
        CancellationToken token;
        lock (_lock)
        {
            if (_started || _disposed) return;
            _started = true;
            token = _disposeSource.Token;
        }

        // Without this, the tracing Activity that is current at bootstrap would be the parent of every fetch.
        var restoreFlow = !ExecutionContext.IsFlowSuppressed();
        if (restoreFlow)
        {
            ExecutionContext.SuppressFlow();
        }

        try
        {
            _ = Task.Run(() => RunAsync(token));
        }
        finally
        {
            if (restoreFlow)
            {
                ExecutionContext.RestoreFlow();
            }
        }
    }

    /// <summary>
    /// Wakes the loop to compute the next poll again, because the new config can have a shorter poll interval.
    /// </summary>
    internal void Reschedule() => WakeUp();

    /// <summary>
    /// Whether a wake-up is set that the loop has not taken yet.
    /// </summary>
    internal bool HasPendingWakeUp => Volatile.Read(ref _wakeUp).Task.IsCompleted;

    /// <summary>
    /// Whether <see cref="Start"/> started the loop.
    /// </summary>
    internal bool IsStarted
    {
        get
        {
            lock (_lock)
            {
                return _started;
            }
        }
    }

    /// <summary>
    /// Fetches the config now because the server has <paramref name="version"/>, for example after a config push.
    /// A version at or below the current one, or at or below a version requested before, is skipped. While no node
    /// returns the version, the loop asks again with a backoff, up to <see cref="MaxNullConfigRetries"/> times.
    /// </summary>
    public void RequestRefresh(ConfigVersion version)
    {
        LogServerPushedVersion(Redactor.SystemData(Name), version);
        lock (_lock)
        {
            if (_latestPushedVersion is { } latest && version <= latest)
            {
                LogSkippingPushedVersion(Redactor.SystemData(Name), version, latest);
                return;
            }

            if (CurrentVersion is { } current && version <= current)
            {
                LogSkippingPushedVersion(Redactor.SystemData(Name), version, current);
                return;
            }

            _latestPushedVersion = version;
        }

        WakeUp();
    }

    /// <summary>
    /// Forgets the pushed versions after a rebootstrap. The rebootstrap can move to a cluster with lower
    /// revisions, and a push from that cluster must not be skipped as already requested. A push at or below
    /// the current version is still skipped.
    /// </summary>
    internal void ForgetPushedVersions()
    {
        lock (_lock)
        {
            _latestPushedVersion = null;
            _pushedVersionsForgotten = true;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        // Ends a fetch or a sleep in progress, and the loop leaves.
        _disposeSource.Cancel();
        _disposeSource.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        _lastPoll = _timeProvider.GetTimestamp();
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var wakeUp = RenewWakeUp();
                try
                {
                    await RunOnceAsync(wakeUp, token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not DocumentNotFoundException && !token.IsCancellationRequested)
                {
                    // The loop must go on. The wait stops a repeated error from spinning it.
                    LogRefreshFailed(ex, Redactor.SystemData(Name));
                    var pollInterval = Context.ClusterOptions.ConfigPollInterval;
                    var delay = pollInterval > TimeSpan.Zero ? pollInterval : DefaultErrorDelay;
                    await SleepAsync(wakeUp, delay, token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
        }
        catch (DocumentNotFoundException)
        {
            LogSwitchingToHttpStreaming(Redactor.SystemData(Name));
            StartHttpStreaming();
        }

        LogLoopExited(Redactor.SystemData(Name));
    }

    /// <summary>
    /// Sleeps until the next fetch is due or a request comes, or runs one fetch pass.
    /// </summary>
    private async Task RunOnceAsync(Task wakeUp, CancellationToken token)
    {
        var latestPushedVersion = GetLatestPushedVersion();
        var currentVersion = CurrentVersion;
        var pendingVersion = GetPendingVersion(latestPushedVersion, currentVersion);

        // Polling is off for an interval of zero or less, InfiniteTimeSpan included, since a poll would be due every turn.
        var pollInterval = PollInterval;
        var untilPoll = Context.ClusterOptions.EnableConfigPolling && pollInterval > TimeSpan.Zero
            ? pollInterval - _timeProvider.GetElapsedTime(_lastPoll)
            : Never;
        var untilRetry = GetTimeUntilRetry(pendingVersion);
        var pollDue = untilPoll <= TimeSpan.Zero;
        var pushDue = untilRetry <= TimeSpan.Zero;
        if (!pollDue && !pushDue)
        {
            await SleepAsync(wakeUp, untilPoll < untilRetry ? untilPoll : untilRetry, token).ConfigureAwait(false);
            return;
        }

        var kvNodes = Nodes.Where(node => node.HasKv).ToList();
        var liveNodes = kvNodes.Where(node => !node.IsDead).ToList();

        // A node that never had a connection cannot answer, so it is not asked. As in the Java SDK, it counts as a failed
        // fetch once its connect has failed. While it is still in its first attempt, the pass cannot fail.
        // A node that had a connection and lost it is asked, and its failed fetch counts.
        var nodes = liveNodes.Where(node => !node.IsAwaitingFirstConnection).ToList().Shuffle();
        var failedFirstConnects = 0;
        var firstAttempts = 0;
        foreach (var node in liveNodes)
        {
            if (node.IsAwaitingFirstConnection)
            {
                if (node.HasFailedFirstConnect)
                {
                    failedFirstConnects++;
                }
                else
                {
                    firstAttempts++;
                }
            }
        }

        if (pollDue)
        {
            // Restarted here as well, so a poll that gets no answer waits a full interval before the next one.
            _lastPoll = _timeProvider.GetTimestamp();
            if (!pushDue && !ShouldPoll(nodes))
            {
                LogPollSkipped(Redactor.SystemData(Name));
                return;
            }
        }

        if (kvNodes.Count == 0)
        {
            // The cluster refresher has no node once buckets own every KV node. The bucket refreshers fetch then.
            LogNoKvNodes(Redactor.SystemData(Name));
        }
        else if (liveNodes.Count == 0)
        {
            // Every node is dead, which counts as every fetch failing.
            LogNoLiveKvNodes(Redactor.SystemData(Name));
            SignalRefreshFailed();
        }
        else if (nodes.Count == 0 && firstAttempts > 0)
        {
            // The pass asks no node. A pushed version still goes through the retry delays below, so the loop does not spin.
            LogNoConnectedKvNodes(Redactor.SystemData(Name), firstAttempts);
        }

        var (answered, config) = await FetchAsync(nodes, failedFirstConnects, firstAttempts, currentVersion,
            pendingVersion, token).ConfigureAwait(false);
        if (answered)
        {
            _lastPoll = _timeProvider.GetTimestamp();
        }

        if (pendingVersion is { } pending)
        {
            TrackPushedVersion(pending, currentVersion, config);
        }

        if (config is null)
        {
            return;
        }

        if (currentVersion is null || config.ConfigVersion > currentVersion)
        {
            LogConfigPublished(Redactor.SystemData(Name), config.ConfigVersion, currentVersion);
            Publish(config);
        }
        else if (pendingVersion is { } pushed && config.ConfigVersion < pushed)
        {
            LogFetchedConfigOlder(Redactor.SystemData(Name), config.ConfigVersion, pushed);
        }
    }

    /// <summary>
    /// Asks the nodes in turn until one answers. While a pushed version is pending, the pass goes on until a node
    /// returns that version, because the node that pushed it can be ahead of the others.
    /// </summary>
    /// <remarks>
    /// Each of <paramref name="failedFirstConnects"/> is a node not asked whose first connect failed, and it counts as
    /// a failed fetch. Each of <paramref name="firstAttempts"/> is a node not asked that is still in its first connect
    /// attempt. The pass signals a failed refresh when no node answered, at least one fetch failed, and no node is
    /// still in its first connect attempt.
    /// </remarks>
    /// <returns>Whether a node answered, and the newest config the nodes returned.</returns>
    private async Task<(bool Answered, BucketConfig? Config)> FetchAsync(List<IClusterNode> nodes,
        int failedFirstConnects, int firstAttempts, ConfigVersion? currentVersion, ConfigVersion? pendingVersion,
        CancellationToken token)
    {
        var timeout = GetFetchTimeout(Context.ClusterOptions.ConfigPollInterval);
        BucketConfig? newest = null;
        var behind = 0;
        var failed = 0;
        var keyNotFound = 0;
        foreach (var node in nodes)
        {
            try
            {
                var knownVersion = newest?.ConfigVersion ?? currentVersion;
                LogFetching(Redactor.SystemData(Name), Redactor.SystemData(node.EndPoint), knownVersion);
                var config = await node.GetClusterMap(knownVersion, timeout, token).ConfigureAwait(false);
                if (config is not null && (newest is null || config.ConfigVersion > newest.ConfigVersion))
                {
                    newest = config;
                }

                if (pendingVersion is null || newest?.ConfigVersion >= pendingVersion)
                {
                    return (true, newest);
                }

                behind++;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                // One KeyNotFound counts as a failed node. The switch needs that answer from every node asked in the pass.
                if (ex is DocumentNotFoundException && ++keyNotFound == nodes.Count)
                {
                    // KV does not serve the config, so the loop ends and switches to HTTP streaming.
                    throw;
                }

                // A failed node does not end the pass, so the next node can still answer.
                failed++;
                LogConfigFetchFailed(ex, Redactor.SystemData(Name), Redactor.SystemData(node.EndPoint));
            }
        }

        if (failed + failedFirstConnects > 0 && failed == nodes.Count)
        {
            if (firstAttempts == 0)
            {
                LogAllNodesFailed(Redactor.SystemData(Name), failed, failedFirstConnects);
                SignalRefreshFailed();
            }
            else
            {
                LogRefreshFailureHeldBack(Redactor.SystemData(Name), firstAttempts);
            }
        }

        if (pendingVersion is { } version)
        {
            LogPushedVersionNotFound(Redactor.SystemData(Name), version, currentVersion, behind, failed);
        }

        return (behind > 0, newest);
    }

    /// <summary>
    /// After a pass for a pushed version, either marks the version done or sets when to ask again.
    /// </summary>
    private void TrackPushedVersion(ConfigVersion pending, ConfigVersion? currentVersion, BucketConfig? fetched)
    {
        if (fetched?.ConfigVersion >= pending)
        {
            MarkPushedVersionHandled(pending);
        }
        else if (_retries.TryNextRetry(pending, currentVersion, out var delay))
        {
            LogRetryingPushedVersion(Redactor.SystemData(Name), pending, _retries.Retries, delay);
            _retryVersion = pending;
            _retryFrom = _timeProvider.GetTimestamp();
            _retryDelay = delay;
        }
        else
        {
            LogGaveUpOnPushedVersion(Redactor.SystemData(Name), pending, _retries.Retries);
            MarkPushedVersionHandled(pending);
        }
    }

    private void MarkPushedVersionHandled(ConfigVersion version)
    {
        _handledPushedVersion = version;
        _retryVersion = null;
        _retries.Reset();
    }

    /// <summary>
    /// The pushed version the loop still has to fetch. Null when the client has it, or the loop is done with it.
    /// </summary>
    private ConfigVersion? GetPendingVersion(ConfigVersion? latestPushedVersion, ConfigVersion? currentVersion) =>
        latestPushedVersion is { } pushed && pushed > _handledPushedVersion && !(currentVersion >= pushed)
            ? pushed
            : null;

    /// <summary>
    /// Zero for a pushed version not asked for yet, so it is fetched at once.
    /// </summary>
    private TimeSpan GetTimeUntilRetry(ConfigVersion? pendingVersion)
    {
        if (pendingVersion is not { } pending)
        {
            return Never;
        }

        return _retryVersion == pending
            ? _retryDelay - _timeProvider.GetElapsedTime(_retryFrom)
            : TimeSpan.Zero;
    }

    /// <summary>
    /// With push config on, the server pushes each config change, so a poll only looks for a change it missed.
    /// With no node to ask, nothing pushes.
    /// </summary>
    private bool ShouldPoll(List<IClusterNode> nodes) =>
        Context.ClusterOptions.Experiments.EnablePollingWithPushConfig
        || nodes.Count == 0
        || nodes.Any(node => node.ServerFeatures?.ClustermapChangeNotificationBrief != true);

    /// <summary>
    /// After <see cref="ForgetPushedVersions"/>, also drops the marks the loop keeps for pushed versions.
    /// </summary>
    private ConfigVersion? GetLatestPushedVersion()
    {
        lock (_lock)
        {
            // The loop drops them itself. A pass that runs during the reset marks its old version when it ends.
            if (_pushedVersionsForgotten)
            {
                _pushedVersionsForgotten = false;
                MarkPushedVersionHandled(default);
            }

            return _latestPushedVersion;
        }
    }

    private async Task SleepAsync(Task wakeUp, TimeSpan duration, CancellationToken token)
    {
        using var sleepSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        var sleep = _timeProvider.Delay(duration < MaxSleep ? duration : MaxSleep, sleepSource.Token);
        await Task.WhenAny(wakeUp, sleep).ConfigureAwait(false);

        // Drops the timer when a request ended the sleep.
        sleepSource.Cancel();
    }

    private Task RenewWakeUp()
    {
        var wakeUp = NewWakeUp();
        Volatile.Write(ref _wakeUp, wakeUp);
        return wakeUp.Task;
    }

    private void WakeUp() => Volatile.Read(ref _wakeUp).TrySetResult(true);

    private static TaskCompletionSource<bool> NewWakeUp() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Counts the retries for one pushed version against one effective version. The count starts again when
    /// either version changes, because a config published by another handler moves the effective version,
    /// and the next attempt is then a new question for the server.
    /// </summary>
    internal struct NullConfigRetryTracker
    {
        private ConfigVersion _pushedVersion;
        private ConfigVersion? _effectiveVersion;

        public int Retries { get; private set; }

        public void Reset() => Retries = 0;

        public bool TryNextRetry(ConfigVersion pushedVersion, ConfigVersion? effectiveVersion, out TimeSpan delay)
        {
            if (!pushedVersion.Equals(_pushedVersion) || !Nullable.Equals(effectiveVersion, _effectiveVersion))
            {
                _pushedVersion = pushedVersion;
                _effectiveVersion = effectiveVersion;
                Retries = 0;
            }

            if (Retries >= MaxNullConfigRetries)
            {
                delay = TimeSpan.Zero;
                return false;
            }

            delay = GetNullConfigRetryDelay(Retries);
            Retries++;
            return true;
        }
    }

    internal static TimeSpan GetNullConfigRetryDelay(int retry)
    {
        // Doubles from the initial delay up to the maximum. The shift is capped to avoid overflow.
        var ticks = InitialNullConfigRetryDelay.Ticks << Math.Min(retry, 20);
        return TimeSpan.FromTicks(Math.Min(ticks, MaxNullConfigRetryDelay.Ticks));
    }

    [LoggerMessage(0, LogLevel.Debug, "New config {fetchedVersion} for {name}, the client had {currentVersion}")]
    private partial void LogConfigPublished(Redacted<string> name, ConfigVersion fetchedVersion, ConfigVersion? currentVersion);

    [LoggerMessage(1, LogLevel.Trace, "Skipping pushed version {pushedVersion} of {name}, the client has or asked for {knownVersion}")]
    private partial void LogSkippingPushedVersion(Redacted<string> name, ConfigVersion pushedVersion, ConfigVersion knownVersion);

    [LoggerMessage(2, LogLevel.Trace, "The server pushed version {pushedVersion} of {name}")]
    private partial void LogServerPushedVersion(Redacted<string> name, ConfigVersion pushedVersion);

    [LoggerMessage(3, LogLevel.Trace, "Fetching the config of {name} from {endpoint}, newer than {knownVersion}")]
    private partial void LogFetching(Redacted<string> name, Redacted<HostEndpointWithPort> endpoint, ConfigVersion? knownVersion);

    [LoggerMessage(4, LogLevel.Trace, "Skipping the poll of {name}, every node pushes config changes")]
    private partial void LogPollSkipped(Redacted<string> name);

    [LoggerMessage(5, LogLevel.Trace, "No node returned pushed version {pushedVersion} of {name}, the client has {currentVersion}, {behind} were behind and {failed} failed")]
    private partial void LogPushedVersionNotFound(Redacted<string> name, ConfigVersion pushedVersion, ConfigVersion? currentVersion, int behind, int failed);

    [LoggerMessage(6, LogLevel.Trace, "Asking again for pushed version {pushedVersion} of {name} in {delay}: retry {retry}")]
    private partial void LogRetryingPushedVersion(Redacted<string> name, ConfigVersion pushedVersion, int retry, TimeSpan delay);

    [LoggerMessage(7, LogLevel.Trace, "No KV node to fetch the config of {name} from")]
    private partial void LogNoKvNodes(Redacted<string> name);

    [LoggerMessage(8, LogLevel.Debug, "The config refresher of {name} stopped")]
    private partial void LogLoopExited(Redacted<string> name);

    [LoggerMessage(9, LogLevel.Warning, "Server returned an older config than was pushed for {name}, fetched {fetchedVersion}, pushed {pushedVersion}")]
    private partial void LogFetchedConfigOlder(Redacted<string> name, ConfigVersion fetchedVersion, ConfigVersion pushedVersion);

    [LoggerMessage(10, LogLevel.Warning, "No live KV node to fetch the config of {name} from")]
    private partial void LogNoLiveKvNodes(Redacted<string> name);

    [LoggerMessage(11, LogLevel.Warning, "No node returned pushed version {pushedVersion} of {name} after {retries} retries. Waiting for the next push or poll.")]
    private partial void LogGaveUpOnPushedVersion(Redacted<string> name, ConfigVersion pushedVersion, int retries);

    [LoggerMessage(12, LogLevel.Warning, "Fetching the config of {name} from {endpoint} failed")]
    private partial void LogConfigFetchFailed(Exception exception, Redacted<string> name, Redacted<HostEndpointWithPort> endpoint);

    [LoggerMessage(13, LogLevel.Warning, "Every node failed to return the config of {name}, {failedFetches} fetches failed and {failedConnects} nodes failed their first connect")]
    private partial void LogAllNodesFailed(Redacted<string> name, int failedFetches, int failedConnects);

    [LoggerMessage(15, LogLevel.Warning, "Refreshing the config of {name} failed")]
    private partial void LogRefreshFailed(Exception exception, Redacted<string> name);

    [LoggerMessage(17, LogLevel.Debug, "No KV node to fetch the config of {name} from, {connecting} nodes have not connected yet")]
    private partial void LogNoConnectedKvNodes(Redacted<string> name, int connecting);

    [LoggerMessage(18, LogLevel.Debug, "No node returned the config of {name}, but {connecting} nodes are still in their first connect, so the refresh does not count as failed")]
    private partial void LogRefreshFailureHeldBack(Redacted<string> name, int connecting);

    [LoggerMessage(16, LogLevel.Information, "KV does not serve the config of {name}, switching to HTTP streaming")]
    private partial void LogSwitchingToHttpStreaming(Redacted<string> name);
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
