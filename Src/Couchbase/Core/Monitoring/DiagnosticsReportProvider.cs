using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Couchbase.Analytics;
using Couchbase.Configuration;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Views;

namespace Couchbase.Core.Monitoring
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

        internal static IPingReport CreatePingReport(string reportId, IConfigInfo config, ICollection<ServiceType> serviceTypes)
        {
            serviceTypes = serviceTypes as List<ServiceType> ?? serviceTypes.ToList();
            if (!serviceTypes.Any())
            {
                serviceTypes = AllServiceTypes;
            }

            var configRev = config.Servers.First().Revision;
            var endpoints = GetEndpointDiagnostics(true, serviceTypes, config);
            return new PingReport(reportId, configRev, endpoints);
        }

        internal static IDiagnosticsReport CreateDiagnosticsReport(string reportId, IEnumerable<IConfigInfo> configs)
        {
            var endpoints = GetEndpointDiagnostics(false, AllServiceTypes, configs.ToArray());
            return new DiagnosticsReport(reportId, endpoints);
        }

        internal static Dictionary<string, IEnumerable<IEndpointDiagnostics>> GetEndpointDiagnostics(bool ping, ICollection<ServiceType> serviceTypes, params IConfigInfo[] configs)
        {
            var now = DateTime.UtcNow;

            return configs
                .Where(config => config.Servers.Any())
                .Select(config =>
                {
                    var endpoints = new Dictionary<string, IEnumerable<IEndpointDiagnostics>>();
                    if (serviceTypes.Contains(ServiceType.KeyValue) && config.Servers.Any(server => server.IsDataNode))
                    {
                        var noopBytes = new Noop(DefaultTranscoder, 0).Write();
                        endpoints.Add("kv", config.Servers.Where(server => server.IsDataNode)
                            .SelectMany(server => server.ConnectionPool.Connections)
                            .Select(connection =>
                            {
                                var endpoint = CreateEndpointHealth(config.BucketName, now, connection);
                                if (ping)
                                {
                                    RecordLatency(endpoint, () => connection.Send(noopBytes));
                                }

                                return endpoint;
                            })
                        );
                    }

                    if (serviceTypes.Contains(ServiceType.Views) && config.Servers.Any(server => server.IsViewNode))
                    {
                        var viewQuery = new ViewQuery("p", "p", "p");
                        endpoints.Add("view", config.Servers.Where(server => server.IsViewNode)
                            .Select(server =>
                            {
                                var endpoint = CreateEndpointHealth(config.BucketName, now, server.ViewClient, server.EndPoint);
                                if (ping)
                                {
                                    RecordLatency(endpoint, () =>
                                    {
                                        var result = server.ViewClient.Execute<dynamic>(viewQuery);
                                        if (result.Exception != null)
                                        {
                                            throw result.Exception;
                                        }
                                    });
                                }

                                return endpoint;
                            })
                        );
                    }

                    if (serviceTypes.Contains(ServiceType.Query) && config.Servers.Any(server => server.IsQueryNode))
                    {
                        var n1qlQuery = new QueryRequest("SELECT 1;");
                        endpoints.Add("n1ql", config.Servers.Where(server => server.IsQueryNode)
                            .Select(server =>
                            {
                                var endpoint = CreateEndpointHealth(config.BucketName, now, server.QueryClient, server.EndPoint);
                                if (ping)
                                {
                                    RecordLatency(endpoint, () =>
                                    {
                                        var result = server.QueryClient.Query<dynamic>(n1qlQuery);
                                        if (result.Exception != null)
                                        {
                                            throw result.Exception;
                                        }
                                    });
                                }

                                return endpoint;
                            })
                        );

                    }

                    if (serviceTypes.Contains(ServiceType.Search) && config.Servers.Any(server => server.IsSearchNode))
                    {
                        var searchQuery = new SearchQuery { Index = "ping" };
                        endpoints.Add("fts", config.Servers.Where(server => server.IsSearchNode)
                            .Select(server =>
                            {
                                var endpoint = CreateEndpointHealth(config.BucketName, now, server.SearchClient, server.EndPoint);
                                if (ping)
                                {
                                    RecordLatency(endpoint, () =>
                                    {
                                        var result = server.SearchClient.Query(searchQuery);
                                        if (result.Exception != null)
                                        {
                                            throw result.Exception;
                                        }
                                    });
                                }

                                return endpoint;
                            })
                        );
                    }

                    if (serviceTypes.Contains(ServiceType.Analytics) && config.Servers.Any(server => server.IsAnalyticsNode))
                    {
                        var analyticsRequest = new AnalyticsRequest("SELECT 1;");
                        endpoints.Add("cbas", config.Servers.Where(server => server.IsAnalyticsNode)
                            .Select(server =>
                            {
                                var endpoint = CreateEndpointHealth(config.BucketName, now, server.AnalyticsClient, server.EndPoint);
                                if (ping)
                                {
                                    RecordLatency(endpoint, () =>
                                    {
                                        var result = server.AnalyticsClient.Query<dynamic>(analyticsRequest);
                                        if (result.Exception != null)
                                        {
                                            throw result.Exception;
                                        }
                                    });
                                }

                                return endpoint;
                            })
                        );
                    }

                    return endpoints;
                })
                .SelectMany(d => d)
                .ToLookup(x => x.Key, x => x.Value)
                .ToDictionary(group => group.Key, group => group.SelectMany(value => value));
        }

        private static EndpointDiagnostics CreateEndpointHealth(string bucketName, DateTime createdAt, IConnection connection)
        {
            return new EndpointDiagnostics
            {
                Id = connection.Identity.ToString(),
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

        internal static EndpointDiagnostics CreateEndpointHealth(string bucketName, DateTime createdAt, IQueryClient client, EndPoint endPoint)
        {
            return new EndpointDiagnostics
            {
                Type = ServiceType.Query,
                LastActivity = CalculateLastActivity(createdAt, client.LastActivity),
                Remote = endPoint?.ToString() ?? UnknownEndpointValue,
                State = client.LastActivity.HasValue ? ServiceState.Connected : ServiceState.New,
                Scope = bucketName
            };
        }

        internal static EndpointDiagnostics CreateEndpointHealth(string bucketName, DateTime createdAt, IViewClient client, EndPoint endPoint)
        {
            return new EndpointDiagnostics
            {
                Type = ServiceType.Views,
                LastActivity = CalculateLastActivity(createdAt, client.LastActivity),
                Remote = endPoint?.ToString() ?? UnknownEndpointValue,
                State = client.LastActivity.HasValue ? ServiceState.Connected : ServiceState.New,
                Scope = bucketName
            };
        }

        internal static EndpointDiagnostics CreateEndpointHealth(string bucketName, DateTime createdAt, ISearchClient client, EndPoint endPoint)
        {
            return new EndpointDiagnostics
            {
                Type = ServiceType.Search,
                LastActivity = CalculateLastActivity(createdAt, client.LastActivity),
                Remote = endPoint?.ToString() ?? UnknownEndpointValue,
                State = client.LastActivity.HasValue ? ServiceState.Connected : ServiceState.New,
                Scope = bucketName
            };
        }

        internal static EndpointDiagnostics CreateEndpointHealth(string bucketName, DateTime createdAt, IAnalyticsClient client, EndPoint endPoint)
        {
            return new EndpointDiagnostics
            {
                Type = ServiceType.Analytics,
                LastActivity = CalculateLastActivity(createdAt, client.LastActivity),
                Remote = endPoint?.ToString() ?? UnknownEndpointValue,
                State = client.LastActivity.HasValue ? ServiceState.Connected : ServiceState.New,
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
