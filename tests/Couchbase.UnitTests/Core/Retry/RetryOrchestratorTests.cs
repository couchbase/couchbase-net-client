using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DataMapping;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.Exceptions.View;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Core.Retry.Query;
using Couchbase.Core.Retry.Search;
using Couchbase.KeyValue;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.UnitTests.Services.Analytics;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Couchbase.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Couchbase.UnitTests.Core.Retry
{
    public class RetryOrchestratorTests
    {
        public class RetryTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] {new Get<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new Set<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new ReplicaRead<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new GetL<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new GetL<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new MultiLookup<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new Config {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new Observe {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public class NotRetryTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] {new Set<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new Delete {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new Append<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new Prepend<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new MultiMutation<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new Unlock {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new Touch {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new GetT<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new GetL<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }


        [Theory]
        [ClassData(typeof(RetryTestData))]
        public async Task Operation_Throws_Timeout_After_N_Retries_Using_BestEffort_When_NotMyVBucket(
            IOperation operation, Exception exception)
        {
            await AssertRetryAsync(operation, exception);
        }

        private async Task AssertRetryAsync(IOperation op, Exception exp)
        {
            var bucketMock = new Mock<BucketBase>();
            bucketMock.Setup(x => x.SendAsync(op, It.IsAny<CancellationToken>(), It.IsAny<TimeSpan>())).Throws(exp);
            var tokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(2500));
            tokenSource.Token.ThrowIfCancellationRequested();
            try
            {
                await RetryOrchestrator.RetryAsync(bucketMock.Object, op, tokenSource.Token,
                    TimeSpan.FromMilliseconds(2500));
            }
            catch (Exception e)
            {
                Assert.IsType<TimeoutException>(e);
                Assert.True(op.Attempts > 1);
            }
        }

        [Fact]
        private async Task Operation_Succeeds_Without_Retry()
        {
            var op = new Get<dynamic> {RetryStrategy = new BestEffortRetryStrategy()};
            var bucketMock = new Mock<BucketBase>();
            bucketMock.Setup(x => x.SendAsync(op, It.IsAny<CancellationToken>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask);

            var tokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(2500));
            tokenSource.Token.ThrowIfCancellationRequested();

            await RetryOrchestrator.RetryAsync(bucketMock.Object, op, tokenSource.Token,
                TimeSpan.FromMilliseconds(2500));

            Assert.Equal(1u, op.Attempts);
        }

        [Theory]
        [ClassData(typeof(NotRetryTestData))]
        public async Task Non_Idempotent_Fails_Without_Retry(IOperation operation, Exception exception)
        {
            await AssertDoesNotRetryAsync(operation, exception);
        }

        public async Task AssertDoesNotRetryAsync(IOperation op, Exception exp)
        {
            var bucketMock = new Mock<BucketBase>();
            bucketMock.Setup(x => x.SendAsync(op, It.IsAny<CancellationToken>(), It.IsAny<TimeSpan>())).Throws(exp);
            var tokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(2500));
            tokenSource.Token.ThrowIfCancellationRequested();

            try
            {
                await bucketMock.Object.RetryAsync(op, tokenSource.Token, TimeSpan.FromMilliseconds(2500));
            }
            catch (Exception e)
            {
                if (e.GetType() == exp.GetType())
                {
                    //expected
                }
                else
                {
                    throw;
                }
            }

            Assert.True(op.Attempts == 1);
        }

        [Fact]
        public async Task Add_DoesNot_Retry_When_KeyExists()
        {
            var op = new Add<dynamic> { RetryStrategy = new BestEffortRetryStrategy() };
        }

        [Theory]
        [InlineData("query-success.json", HttpStatusCode.OK, null)]
        [InlineData("query-facets.json", HttpStatusCode.OK, null)]
        [InlineData("query-error.json", HttpStatusCode.OK, null)]
        public async Task Test_Search(string file, HttpStatusCode httpStatusCode, Type errorType)
        {
            using (var response = ResourceHelper.ReadResourceAsStream(@"Documents\Search\" + file))
            {
                var buffer = new byte[response.Length];
                response.Read(buffer, 0, buffer.Length);

                var responses = GetResponses(20, buffer, httpStatusCode);
                var client = MockedHttpClients.SearchClient(responses);

                var cts = new CancellationTokenSource();
                cts.CancelAfter(1000);

                var searchQuery = new SearchQuery
                {
                    SearchOptions =  new SearchOptions
                    {
                        Token = cts.Token
                    }
                };

                var searchRequest = new SearchRequest
                {
                    Token = cts.Token,
                    Timeout = TimeSpan.FromMilliseconds(1000),
                    Options = new SearchOptions()
                };

                async Task<IServiceResult> Func()
                {
                    var client1 = client;
                    var query1 = searchQuery;
                    return await client1.QueryAsync(searchQuery, cts.Token);
                }

                try
                {
                    await RetryOrchestrator.RetryAsync(Func, searchRequest);
                    Assert.Null(errorType);
                }
                catch (Exception e)
                {
                    Assert.Equal(errorType, e.GetType());
                }
            }
        }

        [Theory]
        [InlineData("200-success.json", HttpStatusCode.OK, null)]
        [InlineData("404-designdoc-notfound.json", HttpStatusCode.NotFound, typeof(ViewNotFoundException))]
        [InlineData("404-view-notfound.json", HttpStatusCode.NotFound, typeof(ViewNotFoundException))]
        public async Task Test_Views(string file, HttpStatusCode httpStatusCode, Type errorType)
        {
            using (var response = ResourceHelper.ReadResourceAsStream(@"Documents\Views\" + file))
            {
                var buffer = new byte[response.Length];
                response.Read(buffer, 0, buffer.Length);

                var responses = GetResponses(20, buffer, httpStatusCode);
                var client = MockedHttpClients.ViewClient(responses);

                var cts = new CancellationTokenSource();
                cts.CancelAfter(1000);

                var viewQuery = new ViewQuery("default", "beers", "brewery_beers")
                {
                    Token = cts.Token
                };

                async Task<IServiceResult> Func()
                {
                    var client1 = client;
                    return await client1.ExecuteAsync(viewQuery);
                }

                try
                {
                    await RetryOrchestrator.RetryAsync(Func, viewQuery);
                    Assert.Null(errorType);
                }
                catch (Exception e)
                {
                    Assert.Equal(errorType, e.GetType());
                }
            }
        }

        [Theory]
        [InlineData("good-request.json", HttpStatusCode.OK, null, true)]
        [InlineData("syntax-24000.json", HttpStatusCode.BadRequest, typeof(CouchbaseException), true)]
        [InlineData("temp-23000.json", HttpStatusCode.BadRequest, typeof(UnambiguousTimeoutException), true)]
        [InlineData("temp-23003.json", HttpStatusCode.BadRequest, typeof(UnambiguousTimeoutException), true)]
        [InlineData("temp-23007.json", HttpStatusCode.BadRequest, typeof(UnambiguousTimeoutException), true)]
        [InlineData("temp-23000.json", HttpStatusCode.BadRequest, typeof(AmbiguousTimeoutException), false)]
        [InlineData("temp-23003.json", HttpStatusCode.BadRequest, typeof(AmbiguousTimeoutException), false)]
        [InlineData("temp-23007.json", HttpStatusCode.BadRequest, typeof(AmbiguousTimeoutException), false)]
        [InlineData("timeout.json", HttpStatusCode.RequestTimeout, typeof(AmbiguousTimeoutException), false)]
        public async Task Test_Analytics(string file, HttpStatusCode httpStatusCode, Type errorType, bool readOnly)
        {
            using (var response = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\" + file))
            {
                var buffer = new byte[response.Length];
                response.Read(buffer, 0, buffer.Length);

                var responses = GetResponses(20, buffer, httpStatusCode);
                var client = MockedHttpClients.AnalyticsClient(responses);

                var cts = new CancellationTokenSource();
                cts.CancelAfter(1000);

                var options = new AnalyticsOptions().
                    Timeout(TimeSpan.FromSeconds(1000)).CancellationToken(cts.Token).WithReadOnly(readOnly);

                var query = new AnalyticsRequest("SELECT * FROM `bar`;")
                {
                    ClientContextId = options.ClientContextIdValue,
                    NamedParameters = options.NamedParameters,
                    PositionalArguments = options.PositionalParameters,
                    ReadOnly = options.ReadOnlyValue,
                    Idempotent = options.ReadOnlyValue
                };
                query.WithTimeout(options.TimeoutValue);
                query.Priority(options.PriorityValue);
                query.Token = options.Token;

                async Task<IServiceResult> Send()
                {
                    var client1 = client;
                    return await client1.QueryAsync<dynamic>(query, options.Token);
                }

                try
                {
                    var result = (AnalyticsResult<dynamic>)await RetryOrchestrator.RetryAsync(Send, query);
                    Assert.Null(errorType);
                }
                catch (Exception e)
                {
                    Assert.Equal(errorType, e.GetType());
                }
            }
        }

        [Theory]
        [InlineData("4040.json", HttpStatusCode.BadRequest, typeof(UnambiguousTimeoutException), true, true)]
        [InlineData("4050.json", HttpStatusCode.BadRequest, typeof(UnambiguousTimeoutException), true, true)]
        [InlineData("4070.json", HttpStatusCode.BadRequest, typeof(UnambiguousTimeoutException), true, true)]
        [InlineData("5000.json", HttpStatusCode.BadRequest, typeof(InternalServerFailureException), true, false)]
        [InlineData("4040.json", HttpStatusCode.BadRequest, typeof(AmbiguousTimeoutException), false, true)]
        [InlineData("4050.json", HttpStatusCode.BadRequest, typeof(AmbiguousTimeoutException), false, true)]
        [InlineData("4070.json", HttpStatusCode.BadRequest, typeof(AmbiguousTimeoutException), false, true)]
        [InlineData("5000.json", HttpStatusCode.BadRequest, typeof(InternalServerFailureException), false, false)]
        public async Task Test_Query(string file, HttpStatusCode httpStatusCode, Type errorType, bool readOnly, bool canRetry)
        {
            using (var response = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\" + file))
            {
                var buffer = new byte[response.Length];
                response.Read(buffer, 0, buffer.Length);

                var responses = GetResponses(20, buffer, httpStatusCode);
                var client = MockedHttpClients.QueryClient(responses);

                var cts = new CancellationTokenSource();
                cts.CancelAfter(100000);

                var queryOptions = new QueryOptions().
                    ReadOnly(readOnly).
                    WithCancellationToken(cts.Token).
                    Timeout(TimeSpan.FromMilliseconds(100000));

                var request = new QueryRequest
                {
                    Options = queryOptions,
                    Statement = "SELECT * FROM `default`",
                    Timeout = TimeSpan.FromMilliseconds(100),
                    Token = cts.Token
                };

                async Task<IServiceResult> Func()
                {
                    var client1 = client;
                    return await client1.QueryAsync<dynamic>(request.Statement, queryOptions);
                }

                try
                {
                    var result = await RetryOrchestrator.RetryAsync(Func, request);
                }
                catch (Exception e)
                {
                    if (canRetry)
                    {
                        Assert.True(request.Attempts > 0);
                    }
                    else
                    {
                        Assert.True(request.Attempts == 0);
                    }

                    if (e is InvalidOperationException)
                    {
                        throw new Exception($"Failed after {request.Attempts} retries.");
                    }
                    Assert.Equal(errorType, e.GetType());
                }
            }
        }

        private Queue<Task<HttpResponseMessage>> GetResponses(int count, byte[] content, HttpStatusCode statusCode = HttpStatusCode.NotFound)
        {
            var responses = new Queue<Task<HttpResponseMessage>>();
            for (var i = 0; i < count; i++)
            {
                responses.Enqueue(Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new ByteArrayContent(content)
                }));
            }

            return responses;
        }

        private IQueryClient BuildMockedClient([NotNull]Queue<Task<HttpResponseMessage>> responses)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).Returns(() => responses.Dequeue());

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

            var options = new ClusterOptions().WithBucket("default").WithServers("http://localhost:8901")
                .WithLogging(loggerFactory);
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

            return new QueryClient(httpClient, new JsonDataMapper(new DefaultSerializer()), context);
        }
    }
}
