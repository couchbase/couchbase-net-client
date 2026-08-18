using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions.View;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.RateLimiting;
using Couchbase.Views;

#nullable enable

namespace Couchbase.Diagnostics
{
    internal static class DiagnosticsReportProvider
    {
        internal const string UnknownEndpointValue = "Unknown";
        private const long TicksPerMicrosecond = 10;

        // Health endpoints of the HTTP services, same paths the other SDKs ping.
        internal const string AdminPingPath = "/admin/ping";
        internal const string SearchPingPath = "/api/ping";

        private static readonly ServiceType[] AllServiceTypes =
        {
            ServiceType.KeyValue,
            ServiceType.Views,
            ServiceType.Query,
            ServiceType.Search,
            ServiceType.Config,
            ServiceType.Analytics
        };

        internal static async Task<IPingReport> CreatePingReportAsync(ClusterContext context, BucketConfig? config, PingOptions options)
        {
            var bucketNodes = context.GetNodes(config?.Name ?? BucketConfig.GlobalBucketName);
            var endpoints =
                await GetEndpointDiagnosticsAsync(context, bucketNodes, context.Nodes, true, options.ServiceTypesValue,
                   options.Token).ConfigureAwait(false);
            return new PingReport(options.ReportIdValue ?? Guid.NewGuid().ToString(), config?.Rev ?? 0, endpoints);
        }

        internal static async Task<IDiagnosticsReport> CreateDiagnosticsReportAsync(ClusterContext context, string reportId)
        {
            var clusterNodes = context.Nodes;
            var endpoints =
                await GetEndpointDiagnosticsAsync(context, clusterNodes, clusterNodes, false, AllServiceTypes,
                    CancellationToken.None).ConfigureAwait(false);
            return new DiagnosticsReport(reportId, endpoints);
        }

        /// <remarks>
        /// KV and Views are scoped to the nodes owned by the bucket, the other services are cluster scoped. A node
        /// without KV never joins a bucket node set, so scoping them the same way would skip them on an MDS cluster.
        /// </remarks>
        private static async ValueTask<ConcurrentDictionary<string, IEnumerable<IEndpointDiagnostics>>> GetEndpointDiagnosticsAsync(ClusterContext context,
           IEnumerable<IClusterNode> bucketNodes, IEnumerable<IClusterNode> clusterNodes, bool ping,
           ICollection<ServiceType> serviceTypes, CancellationToken token)
        {
            var endpoints = new ConcurrentDictionary<string, IEnumerable<IEndpointDiagnostics>>();

            IOperationConfigurator? operationConfigurator = ping
                ? context.ServiceProvider.GetRequiredService<IOperationConfigurator>()
                : null;

            ICouchbaseHttpClientFactory? httpClientFactory = ping
                ? context.ServiceProvider.GetRequiredService<ICouchbaseHttpClientFactory>()
                : null;

            var pingTasks = new List<Task>();

            foreach (var clusterNode in bucketNodes)
            {
                if (serviceTypes.Contains(ServiceType.KeyValue) && clusterNode.HasKv)
                {
                    // Only created once there is a connection, an empty entry would report a service that has nothing behind it.
                    List<IEndpointDiagnostics>? kvEndpoints = null;

                    foreach (var connection in clusterNode.ConnectionPool.GetConnections())
                    {
                        kvEndpoints ??= (List<IEndpointDiagnostics>) endpoints.GetOrAdd("kv", new List<IEndpointDiagnostics>());

                        var endPointDiagnostics =
                            CreateEndpointHealth(clusterNode.Owner?.Name, DateTime.UtcNow, connection, ping);

                        if (ping)
                        {
                            pingTasks.Add(RecordLatencyAsync(endPointDiagnostics, async () =>
                            {
                                using var op = new Noop();
                                try
                                {
                                    Debug.Assert(operationConfigurator is not null,
                                        $"{nameof(operationConfigurator)} should not be null when {nameof(ping)} is true.");
                                    operationConfigurator!.Configure(op);

                                    using var ctp = token == CancellationToken.None
                                        ? CancellationTokenPairSource.FromTimeout(context.ClusterOptions.KvTimeout)
                                        : CancellationTokenPairSource.FromExternalToken(token);
                                    await clusterNode.ExecuteOp(connection, op, ctp.TokenPair).ConfigureAwait(false);
                                }
                                catch (ObjectDisposedException)
                                {
                                    //Ignore as the ping is on a timer is a race condition when the connection is closed
                                }
                                finally
                                {
                                    op.StopRecording();
                                }
                            }, token));
                        }

                        kvEndpoints.Add(endPointDiagnostics);
                    }
                }

                if (serviceTypes.Contains(ServiceType.Views) && clusterNode.HasViews)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    if (clusterNode.Owner is CouchbaseBucket bucket && context.ServiceProvider.IsService<IViewClient>())
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                        var kvEndpoints = (List<IEndpointDiagnostics>) endpoints.GetOrAdd("view", new List<IEndpointDiagnostics>());
                        var endPointDiagnostics = CreateEndpointHealth(bucket.Name, ServiceType.Views, DateTime.UtcNow, clusterNode.LastViewActivity, clusterNode.EndPoint, ping);

                        if (ping)
                        {
                            pingTasks.Add(RecordLatencyAsync(endPointDiagnostics,
#pragma warning disable CS0618 // Type or member is obsolete
                                () => bucket.ViewQueryAsync<object, object>("p", "p"), token));
#pragma warning restore CS0618 // Type or member is obsolete
                        }

                        kvEndpoints.Add(endPointDiagnostics);
                    }
                }
            }

            foreach (var clusterNode in clusterNodes)
            {
                if (serviceTypes.Contains(ServiceType.Query) && clusterNode.HasQuery)
                {
                    AddHttpServiceEndpoint(endpoints, pingTasks, httpClientFactory, "n1ql", ServiceType.Query,
                        clusterNode, AdminPingPath, context.ClusterOptions.QueryTimeout, ping, token);
                }

                if (serviceTypes.Contains(ServiceType.Analytics) && clusterNode.HasAnalytics)
                {
                    AddHttpServiceEndpoint(endpoints, pingTasks, httpClientFactory, "cbas", ServiceType.Analytics,
                        clusterNode, AdminPingPath, context.ClusterOptions.AnalyticsTimeout, ping, token);
                }

                if (serviceTypes.Contains(ServiceType.Search) && clusterNode.HasSearch)
                {
                    AddHttpServiceEndpoint(endpoints, pingTasks, httpClientFactory, "fts", ServiceType.Search,
                        clusterNode, SearchPingPath, context.ClusterOptions.SearchTimeout, ping, token);
                }
            }

            // Await all the pings, if any
            if (pingTasks.Count > 0)
            {
                await Task.WhenAll(pingTasks).ConfigureAwait(false);
            }

            return endpoints;
        }

        /// <summary>
        /// Adds the entry for one HTTP service on one node and, when pinging, the ping of that node's own health endpoint.
        /// </summary>
        /// <remarks>
        /// The ping must go to the node being reported, a load balanced client would let several entries hit the same
        /// node and report the whole service as healthy.
        /// </remarks>
        private static void AddHttpServiceEndpoint(
            ConcurrentDictionary<string, IEnumerable<IEndpointDiagnostics>> endpoints, List<Task> pingTasks,
            ICouchbaseHttpClientFactory? httpClientFactory, string reportKey, ServiceType serviceType,
            IClusterNode clusterNode, string pingPath, TimeSpan fallbackTimeout, bool ping,
            CancellationToken token)
        {
            // The activity must be read first, the service URI getter stamps it with the current time.
            var lastActivity = LastActivity(clusterNode, serviceType);

            // Only a ping needs the service URI, so a diagnostics report leaves the activity alone.
            var pingUri = ping ? BuildPingUri(ServiceUri(clusterNode, serviceType), pingPath) : null;

            var serviceEndpoints = (List<IEndpointDiagnostics>) endpoints.GetOrAdd(reportKey, new List<IEndpointDiagnostics>());
            var endPointDiagnostics = CreateEndpointHealth("Cluster", serviceType, DateTime.UtcNow, lastActivity,
                pingUri?.ToString() ?? clusterNode.EndPoint.ToString(), ping);

            // Without a URI there is nothing to ping, the entry is still reported so it counts as not Ok.
            if (ping && pingUri is not null)
            {
                Debug.Assert(httpClientFactory is not null,
                    $"{nameof(httpClientFactory)} should not be null when {nameof(ping)} is true.");

                pingTasks.Add(RecordLatencyAsync(endPointDiagnostics,
                    () => PingHttpServiceAsync(httpClientFactory!, pingUri, fallbackTimeout, token), token));
            }

            serviceEndpoints.Add(endPointDiagnostics);
        }

        private static DateTime? LastActivity(IClusterNode clusterNode, ServiceType serviceType) => serviceType switch
        {
            ServiceType.Query => clusterNode.LastQueryActivity,
            ServiceType.Analytics => clusterNode.LastAnalyticsActivity,
            ServiceType.Search => clusterNode.LastSearchActivity,
            _ => null
        };

        /// <remarks>
        /// Each of these getters stamps the matching last activity, so only read one when it is needed.
        /// </remarks>
        private static Uri? ServiceUri(IClusterNode clusterNode, ServiceType serviceType) => serviceType switch
        {
            ServiceType.Query => clusterNode.QueryUri,
            ServiceType.Analytics => clusterNode.AnalyticsUri,
            ServiceType.Search => clusterNode.SearchUri,
            _ => null
        };

        /// <summary>
        /// The node's own service URI with the health endpoint path.
        /// </summary>
        internal static Uri? BuildPingUri(Uri? serviceUri, string pingPath) =>
            serviceUri is null
                ? null
                : new UriBuilder(serviceUri) { Path = pingPath, Query = string.Empty }.Uri;

        /// <summary>
        /// Pings a single HTTP service endpoint, anything but a success status code is a failure.
        /// </summary>
        internal static async Task PingHttpServiceAsync(ICouchbaseHttpClientFactory httpClientFactory, Uri pingUri,
            TimeSpan fallbackTimeout, CancellationToken token)
        {
            using var timeoutSource = token == CancellationToken.None
                ? new CancellationTokenSource(fallbackTimeout)
                : null;

            using var httpClient = httpClientFactory.Create();
            using var response = await httpClient.GetAsync(pingUri, timeoutSource?.Token ?? token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        private static EndpointDiagnostics CreateEndpointHealth(string? bucketName, DateTime createdAt, IConnection connection, bool ping)
        {
            return new EndpointDiagnostics
            {
                Id = connection.ConnectionId.ToString(CultureInfo.InvariantCulture),
                Type = ServiceType.KeyValue,
                LastActivity = ping ? null : CalculateLastActivity(createdAt, DateTime.UtcNow - connection.IdleTime),
                Remote = connection.EndPoint.ToString() ?? UnknownEndpointValue,
                Local = connection.LocalEndPoint?.ToString() ?? UnknownEndpointValue,
                EndpointState = connection.EndpointState,
                Scope = bucketName
            };
        }

       internal static EndpointDiagnostics CreateEndpointHealth(string? bucketName, ServiceType serviceType, DateTime createdAt, DateTime? lastActivity,
            HostEndpointWithPort? endPoint, bool ping) =>
            CreateEndpointHealth(bucketName, serviceType, createdAt, lastActivity, endPoint?.ToString(), ping);

        internal static EndpointDiagnostics CreateEndpointHealth(string? bucketName, ServiceType serviceType, DateTime createdAt, DateTime? lastActivity,
            string? remote, bool ping)
        {
            return new EndpointDiagnostics
            {
                Type = serviceType,
                LastActivity = ping ? null : CalculateLastActivity(createdAt, lastActivity),
                Remote = remote ?? UnknownEndpointValue,
                State = lastActivity.HasValue ? ServiceState.Active : ServiceState.New,
                Scope = bucketName
            };
        }

        internal static Task RecordLatencyAsync(EndpointDiagnostics endpoint, Func<Task> action, CancellationToken cancellationToken)
        {
            // Run the action via the global queue to avoid blocking on the synchronous part of the
            // operation, this improves paralellism when there are many pings to perform.
            return Task.Run(async () =>
            {
                var timer = Stopwatch.StartNew();
                try
                {
                    await action().ConfigureAwait(false);
                    endpoint.State = ServiceState.Ok;
                }
                catch (ViewNotFoundException)
                {
                    endpoint.State = ServiceState.Ok;
                }
                catch (RateLimitedException)
                {
                    throw;
                }
                catch (Exception)
                {
                    endpoint.State = ServiceState.Error;
                }

                endpoint.Latency = timer.ElapsedTicks / TicksPerMicrosecond;
            }, cancellationToken);
        }

        internal static long CalculateLastActivity(DateTime createdAt, DateTime? lastActivity)
        {
            if (!lastActivity.HasValue)
            {
                return 0;
            }

            return createdAt.Subtract(lastActivity.Value).Ticks / TicksPerMicrosecond;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
