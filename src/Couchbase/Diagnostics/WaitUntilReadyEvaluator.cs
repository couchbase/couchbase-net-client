using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.Views;

#nullable enable

namespace Couchbase.Diagnostics
{
    /// <summary>
    /// Shared readiness logic for the cluster and bucket level WaitUntilReady, per SDK-RFC 61.
    /// </summary>
    internal static class WaitUntilReadyEvaluator
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
        internal static ISet<ServiceType> ExpectedServices(ClusterContext context, BucketConfig? bucketConfig,
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
                    ServiceType.Query => serviceProvider?.IsService<IQueryClient>() == true,
                    ServiceType.Search => serviceProvider?.IsService<ISearchClient>() == true,
                    ServiceType.Analytics => serviceProvider?.IsService<IAnalyticsClient>() == true,
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
        /// Applies the RFC 61 cluster state rules to a ping report.
        /// </summary>
        internal static ReadinessResult Evaluate(IPingReport report, ICollection<ServiceType> expected, ClusterState desired)
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
                CountEntries(report, serviceType, out var serviceFound, out var serviceOk);

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
            if (!anyMissing && found > 0 && found == ok)
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
        internal static string Describe(IPingReport report, ICollection<ServiceType> expected)
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

                CountEntries(report, serviceType, out var serviceFound, out var serviceOk);
                builder.Append(ReportKey(serviceType) ?? serviceType.ToString());
                builder.Append(serviceFound == 0 ? " missing" : $" {serviceOk}/{serviceFound} ok");
            }

            return builder.ToString();
        }

        private static void CountEntries(IPingReport report, ServiceType serviceType, out int found, out int ok)
        {
            found = 0;
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

        private static HashSet<ServiceType>? AdvertisedFromConfig(BucketConfig? config)
        {
            if (config is null)
            {
                return null;
            }

            List<NodeAdapter> nodes;
            try
            {
                nodes = config.GetNodes();
            }
            catch (Exception)
            {
                // The config has no usable node list yet, the caller falls back to the connected nodes.
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
 *    @copyright 2025 Couchbase, Inc.
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
