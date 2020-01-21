using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Search;

namespace Couchbase.Diagnostics
{
    internal static class DiagnosticsReportProvider
    {
        private const string UnknownEndpointValue = "Unknown";
        private const long TicksPerMicrosecond = 10;
        private static readonly ITypeTranscoder DefaultTranscoder = new DefaultTranscoder();

        private static readonly ServiceType[] AllServiceTypes =
        {
            ServiceType.KeyValue,
            ServiceType.Views,
            ServiceType.Query,
            ServiceType.Search,
            ServiceType.Config,
            ServiceType.Analytics
        };

        internal static IPingReport CreatePingReport(ClusterContext context, BucketConfig config, PingOptions options)
        {
            if (!options.ServiceTypesValue.Any())
            {
               options.ServiceTypes(AllServiceTypes);
            }

            var clusterNodes = context.GetNodes(config.Name);
            var endpoints = GetEndpointDiagnostics(context, clusterNodes,true, options.ServiceTypesValue, CancellationToken.None);
            return new PingReport(options.ReportIdValue, config.Rev, endpoints);
        }

        internal static IDiagnosticsReport CreateDiagnosticsReport(ClusterContext context, string reportId)
        {
            var clusterNodes = context.Nodes;
            var endpoints = GetEndpointDiagnostics(context, clusterNodes.Values,false, AllServiceTypes, CancellationToken.None);
            return new DiagnosticsReport(reportId, endpoints);
        }

       internal static ConcurrentDictionary<string, IEnumerable<IEndpointDiagnostics>> GetEndpointDiagnostics(ClusterContext context, IEnumerable<IClusterNode> clusterNodes, bool ping,
           ICollection<ServiceType> serviceTypes, CancellationToken token)
       {
           var endpoints = new ConcurrentDictionary<string, IEnumerable<IEndpointDiagnostics>>();

           foreach (var clusterNode in clusterNodes)
           {
               if (serviceTypes.Contains(ServiceType.KeyValue) && clusterNode.HasKv)
               {
                   var kvEndpoints = (List<IEndpointDiagnostics>) endpoints.GetOrAdd("kv", new List<IEndpointDiagnostics>());
                   var connection = clusterNode.Connection; //need to make a pool
                   var endPointDiagnostics = CreateEndpointHealth(clusterNode.Owner.Name, DateTime.UtcNow, connection);

                   if (ping)
                   {
                       RecordLatency(endPointDiagnostics, async () =>
                       {
                           var op = new Noop();
                           await clusterNode.ExecuteOp(connection, op, token);
                       });
                   }

                   kvEndpoints.Add(endPointDiagnostics);
               }

               if (serviceTypes.Contains(ServiceType.Views) && clusterNode.HasViews)
               {
                   if (clusterNode.Owner is CouchbaseBucket bucket)
                   {
                       var kvEndpoints = (List<IEndpointDiagnostics>) endpoints.GetOrAdd("view", new List<IEndpointDiagnostics>());
                       var endPointDiagnostics = CreateEndpointHealth(bucket.Name, ServiceType.Views, DateTime.UtcNow, clusterNode.LastViewActivity, clusterNode.EndPoint);

                       if (ping)
                       {
                           RecordLatency(endPointDiagnostics, async () => await bucket.ViewQueryAsync("p", "p"));
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
                       RecordLatency(endPointDiagnostics, () => context.Cluster.QueryAsync<dynamic>("SELECT 1;"));
                   }

                   kvEndpoints.Add(endPointDiagnostics);
               }

               if (serviceTypes.Contains(ServiceType.Analytics) && clusterNode.HasAnalytics)
               {
                   var kvEndpoints = (List<IEndpointDiagnostics>)endpoints.GetOrAdd("cbas", new List<IEndpointDiagnostics>());
                   var endPointDiagnostics = CreateEndpointHealth("Cluster", ServiceType.Analytics, DateTime.UtcNow, clusterNode.LastQueryActivity, clusterNode.EndPoint);

                   if (ping)
                   {
                       RecordLatency(endPointDiagnostics, () => context.Cluster.AnalyticsQueryAsync<dynamic>("SELECT 1;"));
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
                       var searchQuery = new SearchQuery { Index = index };
                       RecordLatency(endPointDiagnostics, () => context.Cluster.SearchQueryAsync(index, searchQuery));
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
                Id = connection.ConnectionId.ToString(),
                Type = ServiceType.KeyValue,
                LastActivity = CalculateLastActivity(createdAt, connection.LastActivity),
                Remote = connection.Socket?.RemoteEndPoint.ToString() ?? UnknownEndpointValue,
                Local = connection.Socket?.LocalEndPoint.ToString() ?? UnknownEndpointValue,
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

        internal static void RecordLatency(EndpointDiagnostics endpoint, Action action)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                action();
                endpoint.State = ServiceState.Ok;
            }
            catch
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
