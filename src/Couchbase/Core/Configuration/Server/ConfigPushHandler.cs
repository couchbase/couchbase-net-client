using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Configuration.Server;

internal class ConfigPushHandler : IDisposable
{
    private readonly ILogger _logger;
    private readonly ClusterNode _node;
    private readonly ClusterContext _context;
    private readonly BlockingCollection<ConfigVersion> _pushedVersions;

    private readonly CancellationTokenSource _continueLoopingSource = new();
    private readonly Task _processLoop;

    public ConfigPushHandler(ClusterNode node, ClusterContext context, ILogger logger)
    {
        // TODO: inject logger via DI rather than using ClusterNode's logger instance.
        _logger = logger;
        _node = node;
        _context = context;

        // create a Producer/Consumer blocking collection backed by a stack, so that entries
        // are processed LIFO.
        _pushedVersions = new BlockingCollection<ConfigVersion>(new ConcurrentStack<ConfigVersion>());
        _processLoop = ProcessConfigPushes();

        // Can this be done with only keeping track of "MaxPushedConfigVersion"?  Maybe, but not trivially.
        // If it is fetched from another node, then GetClusterMap may return null.  If something then interfered
        // with the other node fetching it, this node might never fetch it.
        // TODO: investigate simpler MaxPushedConfigVersion approach.
    }

    private Task ProcessConfigPushes()
    {
        // start a long-running background task to consume the blocking collection.
        return Task.Factory.StartNew(async (_) =>
        {
            foreach (var pushedVersion in _pushedVersions.GetConsumingEnumerable(_continueLoopingSource.Token))
            {
                if (_continueLoopingSource.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    ConfigVersion? currentVersion = null;
                    if (_node.NodesAdapter != null)
                    {
                        currentVersion = _node.NodesAdapter.ConfigVersion;
                        if (pushedVersion < currentVersion)
                        {
                            _logger.LogTrace("{0} skipping push: {1} < {2}", _node.EndPoint, pushedVersion,
                                currentVersion);
                            continue;
                        }

                        if (_pushedVersions.Count > 1)
                        {
                            // We are receiving multiple config push notifications faster than they can be processed.
                            // Back off a little more the more aggressively we are being spammed.
                            // The next time though the loop, we will end up with the latest version and skip the obsolete ones.
                            await Task.Delay(_pushedVersions.Count + 10, _continueLoopingSource.Token)
                                .ConfigureAwait(false);
                        }
                    }

                    using var cts = new CancellationTokenSource(_context.ClusterOptions.KvTimeout);
                    try
                    {
                        var bucketConfig = await _node.GetClusterMap(latestVersionOnClient: currentVersion).ConfigureAwait(false);
                        if (bucketConfig != null)
                        {
                            // GetClusterMap returns null when the config has already been seen by the client
                            // therefore, not-null means this is a new config that must be published to all subscribers.
                            _logger.LogDebug("{0} new config {1} due to config push", _node.EndPoint,
                                bucketConfig.ConfigVersion);
                            _context.PublishConfig(bucketConfig);

                            // Back off in the processing loop.
                            // If configs come back-to-back-to-back, we'll end up piling up a few pushed notifications
                            // Since we're processing LIFO, this lets us avoid requests for configs that will be obsolete
                            // in a trivial amount of time.
                            await Task.Delay(100).ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(LoggingEvents.ConfigEvent, e,
                            "Issue getting Cluster Map on server {Server}!", _node.EndPoint);
                    }
                }
                catch (Exception ex)
                {
                    // catch all exceptions because we don't want to terminate the processing loop
                    _logger.LogWarning("Process config push failed: {0}", ex);
                }
            }

            _logger.LogDebug("Exited consumer loop");
        }, _continueLoopingSource.Token, TaskCreationOptions.LongRunning);
    }

    public void ProcessConfigPush(ConfigVersion configVersion)
    {
        _pushedVersions.Add(configVersion);
    }

    private bool _disposed = false;
    public void Dispose()
    {
        if (_disposed) return;

        // cancel the processing loop, allowing the background task to gracefully finish.
        _continueLoopingSource.Cancel();
        _pushedVersions.Dispose();
        _disposed = true;
    }
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

