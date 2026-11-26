using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Connections;
using Couchbase.Views;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Diagnostics
{
    /// <summary>
    /// Shared readiness logic for the cluster and bucket level WaitUntilReady, per SDK-RFC 61.
    /// </summary>
    internal static partial class WaitUntilReadyEvaluator
    {
        /// <summary>
        /// The services <see cref="DiagnosticsReportProvider"/> is able to ping.
        /// </summary>
        private static readonly ServiceType[] PingableServices =
        {
            ServiceType.KeyValue,
            ServiceType.Views,
            ServiceType.Query,
            ServiceType.Search,
            ServiceType.Analytics
        };

        /// <summary>
        /// The key <see cref="DiagnosticsReportProvider"/> uses for a service in the ping report.
        /// </summary>
        internal static string? ReportKey(ServiceType serviceType) => serviceType switch
        {
            ServiceType.KeyValue => "kv",
            ServiceType.Views => "view",
            ServiceType.Query => "n1ql",
            ServiceType.Analytics => "cbas",
            ServiceType.Search => "fts",
            _ => null
        };

        /// <summary>
        /// The services WaitUntilReady must actually verify.
        /// </summary>
        /// <remarks>
        /// The result must never contain a service the ping report cannot produce an entry for, otherwise
        /// WaitUntilReady waits for something that will never arrive and only ends on timeout.
        /// </remarks>
        internal static ISet<ServiceType> ExpectedServices(ClusterContext? context, BucketConfig? bucketConfig,
            IEnumerable<ServiceType> requested, bool bucketLevel)
        {
            var config = bucketLevel ? bucketConfig : context?.GlobalConfig;
            var advertised = AdvertisedFromConfig(config) ?? AdvertisedFromNodes(context, bucketLevel ? config?.Name : null);
            var serviceProvider = context?.ServiceProvider;

            var expected = new HashSet<ServiceType>();
            foreach (var serviceType in requested)
            {
                if (Array.IndexOf(PingableServices, serviceType) < 0)
                {
                    continue;
                }

                if (!advertised.Contains(serviceType))
                {
                    continue;
                }

                var supported = serviceType switch
                {
                    // Views need an open bucket, the cluster level report never has a view entry.
#pragma warning disable CS0618 // Type or member is obsolete
                    ServiceType.Views => bucketLevel
                                         && HasCouchApi(config)
                                         && serviceProvider?.IsService<IViewClient>() == true,
#pragma warning restore CS0618 // Type or member is obsolete
                    _ => true
                };

                if (supported)
                {
                    expected.Add(serviceType);
                }
            }

            return expected;
        }

        /// <summary>
        /// Pings and evaluates until the desired state is reached or the token ends the wait.
        /// </summary>
        /// <param name="context">The cluster context.</param>
        /// <param name="options">The options given to WaitUntilReady.</param>
        /// <param name="bucketLevel">True for a bucket level wait, false for a cluster level wait.</param>
        /// <param name="currentConfig">The config to evaluate and ping against, read again on every pass.</param>
        /// <param name="canEvaluate">Gate for a pass, false backs off and retries.</param>
        /// <param name="onState">Receives the state of every pass.</param>
        /// <param name="logger">The caller's logger.</param>
        /// <param name="token">Ends the wait, the caller decides what that means.</param>
        internal static async Task PollAsync(
            ClusterContext context,
            WaitUntilReadyOptions options,
            bool bucketLevel,
            Func<BucketConfig?> currentConfig,
            Func<CancellationToken, ValueTask<bool>> canEvaluate,
            Action<ClusterState> onState,
            ILogger logger,
            CancellationToken token)
        {
            var backoff = new WaitUntilReadyBackoff();

            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (!await canEvaluate(token).ConfigureAwait(false))
                {
                    await backoff.DelayAsync(token).ConfigureAwait(false);
                    continue;
                }

                var pass = await EvaluatePassAsync(context, options, bucketLevel, currentConfig(), logger, token)
                    .ConfigureAwait(false);
                onState(pass.Readiness.State);

                if (pass.Readiness.Ready)
                {
                    return;
                }

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    LogStillWaiting(logger, pass.Readiness.State, options.DesiredStateValue,
                        Describe(pass.Report, pass.Expected, pass.KvNodesWithNoConnection));
                }

                await backoff.DelayAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// One pass of the wait, with no delay. It pings the expected services and evaluates the report.
        /// </summary>
        internal static async Task<WaitUntilReadyPass> EvaluatePassAsync(
            ClusterContext context,
            WaitUntilReadyOptions options,
            bool bucketLevel,
            BucketConfig? config,
            ILogger logger,
            CancellationToken token)
        {
            // Recomputed every pass because the topology can change while we wait.
            var requested = options.EffectiveServiceTypes(context).ToList();
            var expected = ExpectedServices(context, config, requested, bucketLevel);

            if (expected.Count == 0)
            {
                LogNoServices(logger, string.Join(", ", requested));
            }

            // A KV node with no connection has no ping entry, so it counts as one entry that is not Ok. The cluster
            // wait counts the cluster nodes. Java pings no KV there, .NET waits for them. The bucket wait counts the
            // nodes of the bucket, as Java pings every node that hosts the bucket. Counted before the ping, so a
            // connect that lands in between gives Degraded for one pass, never Online.
            var kvNodesWithNoConnection = expected.Contains(ServiceType.KeyValue)
                ? CountKvNodesWithNoConnection(KvNodesToCount(context, bucketLevel, config))
                : 0;

            // Ping only what we evaluate, otherwise every pass pays for a service we ignore.
            var report = await DiagnosticsReportProvider.CreatePingReportAsync(context, config,
                new PingOptions
                {
                    ServiceTypesValue = expected.ToList(),
                    Token = token
                }).ConfigureAwait(false);

            var readiness = Evaluate(report, expected, options.DesiredStateValue, kvNodesWithNoConnection);
            return new WaitUntilReadyPass(readiness, report, expected, kvNodesWithNoConnection);
        }

        /// <summary>
        /// The live nodes with KV whose pool has no connection to ping right now.
        /// </summary>
        /// <remarks>
        /// It reads the pool as the ping report does. The two reads are not atomic, so a connection that opens or
        /// closes between them can put a node in both or in neither for one pass.
        /// </remarks>
        internal static int CountKvNodesWithNoConnection(IEnumerable<IClusterNode> nodes)
        {
            var count = 0;
            foreach (var node in nodes)
            {
                if (!node.HasKv || node.IsDead)
                {
                    continue;
                }

                // A pool disposed after the IsDead check belongs to a node that is leaving.
                if (node.ConnectionPool.TryGetConnections(out var connections)
                    && !ConnectionPoolExtensions.HasAny(connections))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// The cluster nodes for a cluster wait. For a bucket wait, the nodes of the bucket, the list its ping reads.
        /// </summary>
        /// <remarks>
        /// A bucket wait with no bucket config counts no node, because its ping then reads every node of the client.
        /// </remarks>
        private static IEnumerable<IClusterNode> KvNodesToCount(ClusterContext context, bool bucketLevel,
            BucketConfig? config)
        {
            if (!bucketLevel)
            {
                return context.Nodes;
            }

            return config is null || config.IsGlobal ? [] : context.GetNodes(config.Name);
        }

        /// <summary>
        /// True once there is something to evaluate, a config with nodes or at least one connected node.
        /// </summary>
        /// <remarks>
        /// Without a topology nothing is advertised, so an empty expected set would make WaitUntilReady succeed
        /// before the bucket is bootstrapped.
        /// </remarks>
        internal static bool HasTopology(BucketConfig? config, IEnumerable<IClusterNode>? nodes)
        {
            if (NodesFromConfig(config) is not null)
            {
                return true;
            }

            if (nodes is null)
            {
                return false;
            }

            using var enumerator = nodes.GetEnumerator();
            return enumerator.MoveNext();
        }

        /// <summary>
        /// Applies the RFC 61 cluster state rules to a ping report.
        /// </summary>
        /// <param name="report">The ping report.</param>
        /// <param name="expected">The services to verify.</param>
        /// <param name="desired">The state the wait wants.</param>
        /// <param name="kvNodesWithNoConnection">KV entries to add to the report, each found and not Ok.</param>
        internal static ReadinessResult Evaluate(IPingReport report, ICollection<ServiceType> expected, ClusterState desired,
            int kvNodesWithNoConnection = 0)
        {
            // Nothing to verify, the caller decides whether that is worth a warning.
            if (expected.Count == 0)
            {
                return new ReadinessResult(ClusterState.Online, true);
            }

            var found = 0;
            var ok = 0;
            var anyMissing = false;
            var allPartlyUp = true;

            foreach (var serviceType in expected)
            {
                CountEntries(report, serviceType, kvNodesWithNoConnection, out var serviceFound, out var serviceOk);

                found += serviceFound;
                ok += serviceOk;

                if (serviceFound == 0)
                {
                    anyMissing = true;
                }

                if (serviceOk == 0)
                {
                    allPartlyUp = false;
                }
            }

            ClusterState state;
            if (!anyMissing && found == ok)
            {
                state = ClusterState.Online;
            }
            else if (allPartlyUp)
            {
                state = ClusterState.Degraded;
            }
            else
            {
                state = ClusterState.Offline;
            }

            var ready = desired switch
            {
                ClusterState.Online => state == ClusterState.Online,
                ClusterState.Degraded => state == ClusterState.Online || state == ClusterState.Degraded,
                _ => false
            };

            return new ReadinessResult(state, ready);
        }

        /// <summary>
        /// A short per service summary of the ping report, for logging why WaitUntilReady is still waiting.
        /// </summary>
        internal static string Describe(IPingReport report, ICollection<ServiceType> expected,
            int kvNodesWithNoConnection = 0)
        {
            if (expected.Count == 0)
            {
                return "none";
            }

            var builder = new StringBuilder();
            foreach (var serviceType in expected)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                CountEntries(report, serviceType, kvNodesWithNoConnection, out var serviceFound, out var serviceOk);
                builder.Append(ReportKey(serviceType) ?? serviceType.ToString());
                builder.Append(serviceFound == 0 ? " missing" : $" {serviceOk}/{serviceFound} ok");
            }

            return builder.ToString();
        }

        private static void CountEntries(IPingReport report, ServiceType serviceType, int kvNodesWithNoConnection,
            out int found, out int ok)
        {
            found = serviceType == ServiceType.KeyValue ? kvNodesWithNoConnection : 0;
            ok = 0;

            var key = ReportKey(serviceType);
            if (key is null || !report.Services.TryGetValue(key, out var entries) || entries is null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                found++;
                if (entry.State == ServiceState.Ok)
                {
                    ok++;
                }
            }
        }

        private static bool HasCouchApi(BucketConfig? config) =>
            config?.BucketCapabilities?.Contains(BucketCapabilities.COUCHAPI) ?? false;

        private static List<NodeAdapter>? NodesFromConfig(BucketConfig? config)
        {
            if (config is null)
            {
                return null;
            }

            try
            {
                return config.GetNodes();
            }
            catch (Exception)
            {
                // Broad on purpose. GetNodes throws ServiceMissingException when it selects no node adapter, and
                // building an adapter from a half filled config can throw anything. Either way there is no usable
                // node list yet, so the caller falls back to the connected nodes.
                return null;
            }
        }

        private static HashSet<ServiceType>? AdvertisedFromConfig(BucketConfig? config)
        {
            var nodes = NodesFromConfig(config);
            if (nodes is null)
            {
                return null;
            }

            var advertised = new HashSet<ServiceType>();
            foreach (var node in nodes)
            {
                Add(advertised, ServiceType.KeyValue, node.IsKvNode);
                Add(advertised, ServiceType.Views, node.IsViewNode);
                Add(advertised, ServiceType.Query, node.IsQueryNode);
                Add(advertised, ServiceType.Search, node.IsSearchNode);
                Add(advertised, ServiceType.Analytics, node.IsAnalyticsNode);
            }

            return advertised;
        }

        private static HashSet<ServiceType> AdvertisedFromNodes(ClusterContext? context, string? bucketName)
        {
            var advertised = new HashSet<ServiceType>();
            if (context?.Nodes is null)
            {
                return advertised;
            }

            foreach (var node in context.GetNodes(bucketName))
            {
                Add(advertised, ServiceType.KeyValue, node.HasKv);
                Add(advertised, ServiceType.Views, node.HasViews);
                Add(advertised, ServiceType.Query, node.HasQuery);
                Add(advertised, ServiceType.Search, node.HasSearch);
                Add(advertised, ServiceType.Analytics, node.HasAnalytics);
            }

            return advertised;
        }

        private static void Add(HashSet<ServiceType> set, ServiceType serviceType, bool present)
        {
            if (present)
            {
                set.Add(serviceType);
            }
        }

        [LoggerMessage(LogLevel.Debug, "WaitUntilReady is {state} but wants {desiredState}, services: {services}")]
        private static partial void LogStillWaiting(ILogger logger, ClusterState state, ClusterState desiredState, string services);

        [LoggerMessage(LogLevel.Warning, "WaitUntilReady has no verifiable service from the requested set {requested}, returning immediately")]
        private static partial void LogNoServices(ILogger logger, string requested);
    }

    /// <summary>
    /// Exponential backoff for the WaitUntilReady poll loops, so a long wait does not keep pinging every node
    /// at a fixed rate.
    /// </summary>
    internal sealed class WaitUntilReadyBackoff
    {
        private const int InitialMilliseconds = 10;
        private const int MaxMilliseconds = 1000;

        private int _milliseconds = InitialMilliseconds;

        /// <summary>
        /// The delay for this pass, doubled for the next one up to the cap.
        /// </summary>
        internal TimeSpan Next()
        {
            var delay = _milliseconds;
            _milliseconds = Math.Min(delay * 2, MaxMilliseconds);
            return TimeSpan.FromMilliseconds(delay);
        }

        internal Task DelayAsync(CancellationToken cancellationToken) => Task.Delay(Next(), cancellationToken);
    }

    /// <summary>
    /// The outcome of one pass of WaitUntilReady.
    /// </summary>
    internal sealed class WaitUntilReadyPass
    {
        public WaitUntilReadyPass(ReadinessResult readiness, IPingReport report, ICollection<ServiceType> expected,
            int kvNodesWithNoConnection)
        {
            Readiness = readiness;
            Report = report;
            Expected = expected;
            KvNodesWithNoConnection = kvNodesWithNoConnection;
        }

        public ReadinessResult Readiness { get; }

        /// <summary>
        /// The ping report as <see cref="DiagnosticsReportProvider"/> built it.
        /// </summary>
        public IPingReport Report { get; }

        public ICollection<ServiceType> Expected { get; }

        /// <summary>
        /// The KV nodes counted as not Ok because they had no connection to ping. Cluster nodes for a cluster wait,
        /// nodes of the bucket for a bucket wait.
        /// </summary>
        public int KvNodesWithNoConnection { get; }
    }

    internal readonly struct ReadinessResult
    {
        public ReadinessResult(ClusterState state, bool ready)
        {
            State = state;
            Ready = ready;
        }

        public ClusterState State { get; }

        public bool Ready { get; }
    }
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
