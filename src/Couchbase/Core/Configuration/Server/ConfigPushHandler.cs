using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Configuration.Server;

internal partial class ConfigPushHandler : IDisposable
{
    private readonly ILogger _logger;
    private readonly ClusterContext _context;
    private readonly TypedRedactor _redactor;
    private readonly CouchbaseBucket _bucket;

    private readonly CancellationTokenSource _continueLoopingSource = new();
    private readonly object _lock = new();
    private readonly SemaphoreSlim _versionReceivedEvent = new(initialCount: 0, maxCount: 1);

    // Version received and not yet processed
    private ConfigVersion _latestVersion;
    private bool _disposed;

    public ConfigPushHandler(CouchbaseBucket bucket, ClusterContext context, ILogger logger, TypedRedactor redactor)
    {
        // TODO: inject logger via DI rather than using ClusterNode's logger instance.
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

        // Can this be done with only keeping track of "MaxPushedConfigVersion"?  Maybe, but not trivially.
        // If it is fetched from another node, then GetClusterMap may return null.  If something then interfered
        // with the other node fetching it, this node might never fetch it.
        // TODO: investigate simpler MaxPushedConfigVersion approach.
    }

    private async Task ProcessConfigPushesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Don't release this semaphore, it is released whenever a new config is received.
            await _versionReceivedEvent.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                ConfigVersion pushedVersion;
                lock (_lock)
                {
                    // This lock prevents tearing of the ConfigVersion struct
                    pushedVersion = _latestVersion;
                }

                // Note: There is a slim chance that the same _latestVersion may be processed twice if a new version
                // is received between the WaitAsync above being triggered and reaching the lock. In this case the second version
                // received is processed here but then on the next loop WaitAsync will immediately return a second time
                // due the semaphore being released again by ProcessConfigPush.

                ConfigVersion? currentVersion = null;
                if (_bucket.CurrentConfig != null)
                {
                    currentVersion = _bucket.CurrentConfig?.ConfigVersion;

                    // Recheck to see if this node received the config version from another node since
                    // the event was queued on this processing thread
                    if (pushedVersion <= currentVersion)
                    {
                        SkippingPush(_redactor.SystemData(_bucket.Name), pushedVersion, currentVersion.GetValueOrDefault());
                        continue;
                    }
                }

                var node = _bucket.Nodes.FirstOrDefault(x => x.HasManagement);
                if (node != null)
                {
                    var bucketConfig = await node
                        .GetClusterMap(latestVersionOnClient: currentVersion, cancellationToken).ConfigureAwait(false);
                    if (bucketConfig != null)
                    {
                        if (bucketConfig.ConfigVersion <= pushedVersion)
                        {
                            // GetClusterMap returns null when the config has already been seen by the client
                            // therefore, not-null means this is a new config that must be published to all subscribers.
                            ConfigPublished(_redactor.SystemData(_bucket.Name), bucketConfig.ConfigVersion,
                                currentVersion);
                            _context.PublishConfig(bucketConfig);
                        }
                    }
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
        // We must use a lock, not Interlocked, because ConfigVersion is a large reference type
        // and tearing could occur. Also, we're doing reads and writes with comparisons.
        lock (_lock)
        {
            // Will always be true at start, when _nextVersion is all zeros. Also ignores any lower
            // versions received without signaling the processing thread.
            if (configVersion <= _latestVersion)
            {
                // Already pushed from to the processing thread previously
                SkippingPush(_redactor.SystemData(_bucket.Name), configVersion, _latestVersion);
                return;
            }

            if (_bucket.CurrentConfig is not null && _bucket.CurrentConfig.ConfigVersion >= configVersion)
            {
                // Already seen by this node via another node's push
                SkippingPush(_redactor.SystemData(_bucket.Name), configVersion, _bucket.CurrentConfig.ConfigVersion);
                return;
            }

            _latestVersion = configVersion;

            try
            {
                _versionReceivedEvent.Release();
            }
            catch (SemaphoreFullException)
            {
                // Ignore this error, it means that we're already waiting on the processing thread
                // to handle a new config version. When it wakes it will see this new version instead.
            }
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
    private partial void ConfigPublished(Redacted<string> bucket, ConfigVersion pushedVersion, ConfigVersion? currentVersion);

    [LoggerMessage(1, LogLevel.Debug, "Skipping push: {bucket} < {pushedVersion} < {currentVersion}")]
    private partial void SkippingPush(Redacted<string> bucket, ConfigVersion pushedVersion,
        ConfigVersion currentVersion);
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

