using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions.View;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Search;
using Couchbase.Search.Queries;

namespace Couchbase.Diagnostics
{
    internal static class DiagnosticsReportProvider
    {
        internal const string UnknownEndpointValue = "Unknown";
        private const long TicksPerMicrosecond = 10;
        private static readonly ITypeTranscoder DefaultTranscoder = new LegacyTranscoder();

        private static readonly ServiceType[] AllServiceTypes =
        {
            ServiceType.KeyValue,
            ServiceType.Views,
            ServiceType.Query,
            ServiceType.Search,
            ServiceType.Config,
            ServiceType.Analytics
        };

        internal static async Task<IPingReport> CreatePingReportAsync(ClusterContext context, BucketConfig config, PingOptions options)
        {
            var clusterNodes = context.GetNodes(config.Name);
            var endpoints =
                await GetEndpointDiagnosticsAsync(context, clusterNodes, true, options.ServiceTypesValue,
                    CancellationToken.None).ConfigureAwait(false);
            return new PingReport(options.ReportIdValue ?? Guid.NewGuid().ToString(), config.Rev, endpoints);
        }

        internal static async Task<IDiagnosticsReport> CreateDiagnosticsReportAsync(ClusterContext context, string reportId)
        {
            var clusterNodes = context.Nodes;
            var endpoints =
                await GetEndpointDiagnosticsAsync(context, clusterNodes, false, AllServiceTypes,
                    CancellationToken.None).ConfigureAwait(false);
            return new DiagnosticsReport(reportId, endpoints);
        }

       private static async Task<ConcurrentDictionary<string, IEnumerable<IEndpointDiagnostics>>> GetEndpointDiagnosticsAsync(ClusterContext context,
           IEnumerable<IClusterNode> clusterNodes, bool ping, ICollection<ServiceType> serviceTypes,CancellationToken token)
       {
           var endpoints = new ConcurrentDictionary<string, IEnumerable<IEndpointDiagnostics>>();

           IOperationConfigurator operationConfigurator = ping
               ? context.ServiceProvider.GetRequiredService<IOperationConfigurator>()
               : null;

           foreach (var clusterNode in clusterNodes)
           {
               if (serviceTypes.Contains(ServiceType.KeyValue) && clusterNode.HasKv)
               {
                   var kvEndpoints = (List<IEndpointDiagnostics>) endpoints.GetOrAdd("kv", new List<IEndpointDiagnostics>());

                   foreach (var connection in clusterNode.ConnectionPool.GetConnections())
                   {
                       var endPointDiagnostics =
                           CreateEndpointHealth(clusterNode.Owner?.Name, DateTime.UtcNow, connection);

                       if (ping)
                       {
                           await RecordLatencyAsync(endPointDiagnostics, async () =>
                           {
                               var op = new Noop();
                               operationConfigurator.Configure(op);

                               using var ctp = CancellationTokenPairSource.FromExternalToken(token);
                               await clusterNode.ExecuteOp(connection, op, ctp.TokenPair).ConfigureAwait(false);
                           }).ConfigureAwait(false);
                       }

                       kvEndpoints.Add(endPointDiagnostics);
                   }
               }

               if (serviceTypes.Contains(ServiceType.Views) && clusterNode.HasViews)
               {
                   if (clusterNode.Owner is CouchbaseBucket bucket)
                   {
                       var kvEndpoints = (List<IEndpointDiagnostics>) endpoints.GetOrAdd("view", new List<IEndpointDiagnostics>());
                       var endPointDiagnostics = CreateEndpointHealth(bucket.Name, ServiceType.Views, DateTime.UtcNow, clusterNode.LastViewActivity, clusterNode.EndPoint);

                       if (ping)
                       {
                           await RecordLatencyAsync(endPointDiagnostics,
                                   async () => await bucket.ViewQueryAsync<object, object>("p", "p").ConfigureAwait(false))
                               .ConfigureAwait(false);
                       }

                       kvEndpoints.Add(endPointDiagnostics);
                   }
               }

               if (serviceTypes.Contains(ServiceType.Query) && clusterNode.HasQuery)
               {
                   var kvEndpoints = (List<IEndpointDiagnostics>)endpoints.GetOrAdd("n1ql", new List<IEndpointDiagnostics>());
                   var endPointDiagnostics = CreateEndpointHealth("Cluster", ServiceType.Query, DateTime.UtcNow, clusterNode.LastQueryActivity, clusterNode.EndPoint);

                   if (ping)
                   {
                       await RecordLatencyAsync(endPointDiagnostics,
                               () => context.Cluster.QueryAsync<dynamic>("SELECT 1;"))
                           .ConfigureAwait(false);
                   }

                   kvEndpoints.Add(endPointDiagnostics);
               }

               if (serviceTypes.Contains(ServiceType.Analytics) && clusterNode.HasAnalytics)
               {
                   var kvEndpoints = (List<IEndpointDiagnostics>)endpoints.GetOrAdd("cbas", new List<IEndpointDiagnostics>());
                   var endPointDiagnostics = CreateEndpointHealth("Cluster", ServiceType.Analytics, DateTime.UtcNow, clusterNode.LastQueryActivity, clusterNode.EndPoint);

                   if (ping)
                   {
                       await RecordLatencyAsync(endPointDiagnostics,
                               () => context.Cluster.AnalyticsQueryAsync<dynamic>("SELECT 1;"))
                           .ConfigureAwait(false);
                   }

                   kvEndpoints.Add(endPointDiagnostics);
               }

               if (serviceTypes.Contains(ServiceType.Search) && clusterNode.HasSearch)
               {
                   var kvEndpoints = (List<IEndpointDiagnostics>)endpoints.GetOrAdd("fts", new List<IEndpointDiagnostics>());
                   var endPointDiagnostics = CreateEndpointHealth("Cluster", ServiceType.Search, DateTime.UtcNow, clusterNode.LastQueryActivity, clusterNode.EndPoint);

                   if (ping)
                   {
                       var index = "ping";
                       await RecordLatencyAsync(endPointDiagnostics,
                           () => context.Cluster.SearchQueryAsync(index, new NoOpQuery())).ConfigureAwait(false);
                   }

                   kvEndpoints.Add(endPointDiagnostics);
               }
           }

           return endpoints;
       }

       private static EndpointDiagnostics CreateEndpointHealth(string bucketName, DateTime createdAt, IConnection connection)
        {
            return new EndpointDiagnostics
            {
                Id = connection.ConnectionId.ToString(CultureInfo.InvariantCulture),
                Type = ServiceType.KeyValue,
                LastActivity = CalculateLastActivity(createdAt, DateTime.UtcNow - connection.IdleTime),
                Remote = connection.EndPoint.ToString() ?? UnknownEndpointValue,
                Local = connection.LocalEndPoint?.ToString() ?? UnknownEndpointValue,
                State = GetConnectionServiceState(connection),
                Scope = bucketName
            };
        }

        internal static ServiceState GetConnectionServiceState(IConnection connection)
        {
            if (!connection.IsConnected)
            {
                return ServiceState.New;
            }

            if (!connection.IsAuthenticated)
            {
                return ServiceState.Authenticating;
            }

            if (!connection.IsDead)
            {
                return ServiceState.Connected;
            }

            return ServiceState.Disconnected;
        }

        internal static EndpointDiagnostics CreateEndpointHealth(string bucketName, ServiceType serviceType, DateTime createdAt, DateTime? lastActivity, EndPoint endPoint)
        {
            return new EndpointDiagnostics
            {
                Type = serviceType,
                LastActivity = CalculateLastActivity(createdAt, lastActivity),
                Remote = endPoint?.ToString() ?? UnknownEndpointValue,
                State = lastActivity.HasValue ? ServiceState.Connected : ServiceState.New,
                Scope = bucketName
            };
        }

        internal static async Task RecordLatencyAsync(EndpointDiagnostics endpoint, Func<Task> action)
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
            catch(Exception)
            {
                endpoint.State = ServiceState.Error;
            }

            endpoint.Latency = timer.ElapsedTicks / TicksPerMicrosecond;
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
