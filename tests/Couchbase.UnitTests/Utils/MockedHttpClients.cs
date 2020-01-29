using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.Utils;
using Couchbase.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace Couchbase.UnitTests.Utils
{
    internal static class MockedHttpClients
    {
        public static IQueryClient QueryClient([NotNull] Queue<Task<HttpResponseMessage>> responses)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).Returns(responses.Dequeue);

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8091")
            };

            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder
                .AddFilter(level => level >= LogLevel.Debug)
            );
            var loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
            loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);

            var options = new ClusterOptions().Bucket("default").Servers("http://localhost:8901")
                .Logging(loggerFactory);
            var context = new ClusterContext(null, options);

            var clusterNode = new ClusterNode(context)
            {
                EndPoint = new Uri("http://localhost:8091").GetIpEndPoint(8091, false),
                NodesAdapter = new NodeAdapter(new Node {Hostname = "127.0.0.1"},
                    new NodesExt
                    {
                        Hostname = "127.0.0.1",
                        Services = new Couchbase.Core.Configuration.Server.Services
                        {
                            N1Ql = 8093
                        }
                    }, new BucketConfig())
            };
            clusterNode.BuildServiceUris();
            context.AddNode(clusterNode);

            var serializer = new DefaultSerializer();
            return new QueryClient(httpClient, new JsonDataMapper(serializer), serializer, context);
        }

        internal static IAnalyticsClient AnalyticsClient([NotNull] Queue<Task<HttpResponseMessage>> responses)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).Returns(responses.Dequeue);

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8091")
            };

            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder
                .AddFilter(level => level >= LogLevel.Debug)
            );
            var loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
            loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);

            var options = new ClusterOptions().Bucket("default").Servers("http://localhost:8901")
                .Logging(loggerFactory);
            var context = new ClusterContext(null, options);

            var clusterNode = new ClusterNode(context)
            {
                EndPoint = new Uri("http://localhost:8091").GetIpEndPoint(8091, false),
                NodesAdapter = new NodeAdapter(new Node { Hostname = "127.0.0.1" },
                    new NodesExt
                    {
                        Hostname = "127.0.0.1",
                        Services = new Couchbase.Core.Configuration.Server.Services
                        {
                            Cbas = 8095
                        }
                    }, new BucketConfig())
            };
            clusterNode.BuildServiceUris();
            context.AddNode(clusterNode);

            var serializer = new DefaultSerializer();
            return new AnalyticsClient(httpClient, new JsonDataMapper(serializer), serializer, context);
        }

        internal static ISearchClient SearchClient([NotNull] Queue<Task<HttpResponseMessage>> responses)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).Returns(responses.Dequeue);

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8094")
            };

            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder
                .AddFilter(level => level >= LogLevel.Debug)
            );
            var loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
            loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);

            var options = new ClusterOptions().Bucket("default").Servers("http://localhost:8901")
                .Logging(loggerFactory);
            var context = new ClusterContext(null, options);

            var clusterNode = new ClusterNode(context)
            {
                EndPoint = new Uri("http://localhost:8091").GetIpEndPoint(8091, false),
                NodesAdapter = new NodeAdapter(new Node { Hostname = "127.0.0.1" },
                    new NodesExt
                    {
                        Hostname = "127.0.0.1",
                        Services = new Couchbase.Core.Configuration.Server.Services
                        {
                            Fts = 8094
                        }
                    }, new BucketConfig())
            };
            clusterNode.BuildServiceUris();
            context.AddNode(clusterNode);

            return new SearchClient(httpClient, new SearchDataMapper(), context);
        }

        internal static IViewClient ViewClient([NotNull] Queue<Task<HttpResponseMessage>> responses)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).Returns(responses.Dequeue);

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8091")
            };

            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder
                .AddFilter(level => level >= LogLevel.Debug)
            );
            var loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
            loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);

            var options = new ClusterOptions().Bucket("default").Servers("http://localhost:8901").Logging(loggerFactory);
            var context = new ClusterContext(null, options);

            var clusterNode = new ClusterNode(context)
            {
                EndPoint = new Uri("http://localhost:8091").GetIpEndPoint(8091, false),
                NodesAdapter = new NodeAdapter(new Node { Hostname = "127.0.0.1" },
                    new NodesExt
                    {
                        Hostname = "127.0.0.1",
                        Services = new Couchbase.Core.Configuration.Server.Services
                        {
                            Cbas = 8094
                        }
                    }, new BucketConfig())
            };
            clusterNode.BuildServiceUris();
            context.AddNode(clusterNode);

            var serializer = new DefaultSerializer();
            return new ViewClient(httpClient, new JsonDataMapper(serializer), serializer, context);
        }
    }
}

