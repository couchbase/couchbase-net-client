using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Configuration.Server;

internal partial class ConfigPushHandler : IDisposable
{
    private readonly ILogger _logger;
    private readonly ClusterContext _context;
    private readonly TypedRedactor _redactor;
    private readonly BucketBase _bucket;

    private readonly CancellationTokenSource _continueLoopingSource = new();
    private readonly object _lock = new();
    private readonly SemaphoreSlim _versionReceivedEvent = new(initialCount: 0, maxCount: 1);

    // Version received and not yet processed
    private ConfigVersion _latestVersion;
    private volatile bool _disposed;

    public ConfigPushHandler(BucketBase bucket, ClusterContext context, ILogger logger, TypedRedactor redactor)
    {
        _logger = logger;
        _bucket = bucket;
        _context = context;
        _redactor = redactor;

        bool restoreFlow = false;
        try
        {
            if (!ExecutionContext.IsFlowSuppressed())
            {
                ExecutionContext.SuppressFlow();
                restoreFlow = true;
            }

            _ = ProcessConfigPushesAsync(_continueLoopingSource.Token);
        }
        finally
        {
            if (restoreFlow)
            {
                ExecutionContext.RestoreFlow();
            }
        }
    }

    private async Task ProcessConfigPushesAsync(CancellationToken cancellationToken)
    {
        ConfigVersion lastSkippedVersion = new(0, 0);
        long skipsWithNoPublish = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            // Don't release this semaphore, it is released whenever a new config is received.
            await _versionReceivedEvent.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var failedConfigFetchNodes = new HashSet<IClusterNode>();

                ConfigVersion pushedVersion;
                lock (_lock)
                {
                    // This lock prevents tearing of the ConfigVersion struct
                    pushedVersion = _latestVersion;

                    if (pushedVersion <= lastSkippedVersion)
                    {
                        // we already skipped this version once and it hasn't been updated.
                        break;
                    }
                }


                LogEnteredSemaphorePush(pushedVersion);

                // Note: There is a slim chance that the same _latestVersion may be processed twice if a new version
                // is received between the WaitAsync above being triggered and reaching the lock. In this case the second version
                // received is processed here but then on the next loop WaitAsync will immediately return a second time
                // due the semaphore being released again by ProcessConfigPush.
                ConfigVersion? effectiveVersion = null;
                if (_bucket.CurrentConfig != null)
                {
                    BucketConfig effectiveConfig = _bucket.CurrentConfig;
                    effectiveVersion = effectiveConfig.ConfigVersion;

                    // Recheck to see if this node received the config version from another node since
                    // the event was queued on this processing thread
                    if (pushedVersion <= effectiveVersion)
                    {
                        lastSkippedVersion = pushedVersion;
                        LogSkippingPush2(_redactor.SystemData(_bucket.Name), pushedVersion,
                            effectiveVersion.GetValueOrDefault());
                        await Task.Yield();

                        // because of a possible race, we continue here and only break the loop in the locked section
                        // at the beginning of this loop.
                        continue;
                    }
                }

                var randomNodes = _bucket.Nodes
                    .Where(node => node is { HasKv: true, IsDead: false})
                    .ToList().Shuffle();
                if (randomNodes is not { Count: > 0 })
                {
                    LogNoLiveKvNodes(pushedVersion);
                    continue;
                }

                BucketConfig fetchedBucketConfig = null;
                foreach (var node in randomNodes)
                {
                    if (node is null) continue;
                    try
                    {
                        fetchedBucketConfig = await node
                            .GetClusterMap(latestVersionOnClient: effectiveVersion, cancellationToken)
                            .ConfigureAwait(false);
                        if (fetchedBucketConfig is not null)
                        {
                            break;
                        }
                    }
                    catch (SocketNotAvailableException)
                    {
                        failedConfigFetchNodes.Add(node);
                        _logger.LogWarning("Socket closed on {EndPoint} retrying on next random node",
                            node.EndPoint);
                        await Task.Yield();
                    }
                }

                if (failedConfigFetchNodes.Count == randomNodes.Count)
                {
                    _logger.LogCritical("All {count} nodes failed to fetch config.", randomNodes.Count);
                }

                if (fetchedBucketConfig != null)
                {
                    var fetchedVersion = fetchedBucketConfig.ConfigVersion;
                    if (fetchedVersion > effectiveVersion)
                    {
                        // GetClusterMap returns null when the config has already been seen by the client
                        // therefore, not-null means this is a new config that must be published to all subscribers.
                        LogConfigPublished(_redactor.SystemData(_bucket.Name), fetchedBucketConfig.ConfigVersion,
                            effectiveVersion);
                        Interlocked.Exchange(ref skipsWithNoPublish, 0);
                        _context.PublishConfig(fetchedBucketConfig);
                    }
                    else if (fetchedVersion < pushedVersion)
                    {
                        LogFetchedConfigOlder(_redactor.SystemData(_bucket.Name), fetchedVersion,
                        pushedVersion);
                    }
                    else
                    {
                        LogSkippingPush2(_redactor.SystemData(_bucket.Name), fetchedVersion,
                            effectiveVersion.GetValueOrDefault());
                        continue;
                    }

                    ConfigVersion nextVersion = _latestVersion;

                    if (nextVersion > fetchedVersion)
                    {
                        // a new version came in while we were publishing.
                        LogAttemptedButSkipped(nextVersion, fetchedVersion);
                        var skips = Interlocked.Increment(ref skipsWithNoPublish);
                        if (skips > 100)
                        {
                            await Task.Delay(10).ConfigureAwait(false);
                        }

                        TryReleaseNewVersionSemaphore();
                    }
                }
                else
                {
                    LogServerReturnedNullConfig(pushedVersion, effectiveVersion.GetValueOrDefault());
                }
            }
            catch (Exception ex)
            {
                // catch all exceptions because we don't want to terminate the processing loop
                _logger.LogWarning(ex, "Process config push failed");
            }
        }

        _logger.LogDebug("Exited consumer loop");
    }

    public void ProcessConfigPush(ConfigVersion configVersion)
    {
        LogServerPushedVersion(configVersion);

        // We must use a lock, not Interlocked, because ConfigVersion is a large reference type
        // and tearing could occur. Also, we're doing reads and writes with comparisons.
        lock (_lock)
        {
            // Will always be true at start, when _nextVersion is all zeros. Also ignores any lower
            // versions received without signaling the processing thread.
            if (configVersion <= _latestVersion)
            {
                // Already pushed from to the processing thread previously
                LogSkippingPush(_redactor.SystemData(_bucket.Name), configVersion, _latestVersion);
                return;
            }

            if (_bucket.CurrentConfig is not null && configVersion <= _bucket.CurrentConfig.ConfigVersion)
            {
                // Already seen by this node via another node's push
                LogSkippingPush(_redactor.SystemData(_bucket.Name), configVersion, _bucket.CurrentConfig.ConfigVersion);
                return;
            }

            LogUpdatedLatestVersion(_latestVersion, configVersion);
            _latestVersion = configVersion;

            TryReleaseNewVersionSemaphore();
        }
    }

    private void TryReleaseNewVersionSemaphore()
    {
        try
        {
            _versionReceivedEvent.Release();
        }
        catch (SemaphoreFullException)
        {
            _logger.LogDebug("Tried to release VersionReceivedEvent but we are already waiting on the processing thread");
            // Ignore this error, it means that we're already waiting on the processing thread
            // to handle a new config version. When it wakes it will see this new version instead.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        // cancel the processing loop, allowing the background task to gracefully finish.
        _continueLoopingSource.Cancel();
        _continueLoopingSource.Dispose();
        _disposed = true;
    }

    [LoggerMessage(0, LogLevel.Debug, "{bucket} new config {pushedVersion} due to config push - old {currentVersion}")]
    private partial void LogConfigPublished(Redacted<string> bucket, ConfigVersion pushedVersion, ConfigVersion? currentVersion);

    [LoggerMessage(1, LogLevel.Trace, "Skipping push: {bucket} < {pushedVersion} < {currentVersion}")]
    private partial void LogSkippingPush(Redacted<string> bucket, ConfigVersion pushedVersion, ConfigVersion currentVersion);

    [LoggerMessage(2, LogLevel.Trace, "Entered the semaphore to check pushedVersion {pushedVersion}")]
    private partial void LogEnteredSemaphorePush(ConfigVersion pushedVersion);

    [LoggerMessage(3, LogLevel.Trace, "The server pushed configVersion: {configVersion}")]
    private partial void LogServerPushedVersion(ConfigVersion configVersion);

    [LoggerMessage(4, LogLevel.Trace, "Updating the latest configVersion {latestVersion} to the {configVersion}")]
    private partial void LogUpdatedLatestVersion(ConfigVersion latestVersion, ConfigVersion configVersion);

    [LoggerMessage(5, LogLevel.Trace, "Skipping push: {bucket} < {pushedVersion} < {currentVersion}")]
    private partial void LogSkippingPush2(Redacted<string> bucket, ConfigVersion pushedVersion, ConfigVersion currentVersion);

    [LoggerMessage(6, LogLevel.Trace, "Trying again: the next version is {nextVersion} is greater the published {publishedVersion}")]
    private partial void LogAttemptedButSkipped(ConfigVersion nextVersion, ConfigVersion publishedVersion);

    [LoggerMessage(7, LogLevel.Trace,"The server returned null for pushed version {pushedVersion} currentVersion: {effectiveVersion}")]
    private partial void LogServerReturnedNullConfig(ConfigVersion pushedVersion, ConfigVersion effectiveVersion);

    [LoggerMessage(8, LogLevel.Trace, "The node was null for {effectiveVersion}")]
    private partial void LogNodeWasNull(ConfigVersion effectiveVersion);

    [LoggerMessage(9, LogLevel.Warning, "Server returned older config than was pushed: {bucket}, fetched={fetchedVersion}, pushed={pushedVersion}")]
    private partial void LogFetchedConfigOlder(Redacted<string> bucket, ConfigVersion fetchedVersion, ConfigVersion pushedVersion);

    [LoggerMessage(10, LogLevel.Warning, "No live KV nodes were found while processing {configVersion}")]
    private partial void LogNoLiveKvNodes(ConfigVersion configVersion);
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2023 Couchbase, Inc.
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
