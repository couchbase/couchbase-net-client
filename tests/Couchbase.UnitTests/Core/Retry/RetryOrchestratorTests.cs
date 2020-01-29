using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.View;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.Retry;
using Couchbase.Core.Retry.Query;
using Couchbase.Core.Retry.Search;
using Couchbase.KeyValue;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Moq;
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
            object operation, Exception exception)
        {
            await AssertRetryAsync((IOperation) operation, exception);
        }

        private async Task AssertRetryAsync(IOperation op, Exception exp)
        {
            var retryOrchestrator = CreateRetryOrchestrator();

            var bucketMock = new Mock<BucketBase>("fake", new ClusterContext(), new Mock<IScopeFactory>().Object, retryOrchestrator, new Mock<ILogger>().Object);
            bucketMock.Setup(x => x.SendAsync(op, It.IsAny<CancellationToken>(), It.IsAny<TimeSpan>())).Throws(exp);
            var tokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(2500));
            tokenSource.Token.ThrowIfCancellationRequested();
            try
            {
                await retryOrchestrator.RetryAsync(bucketMock.Object, op, tokenSource.Token,
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
            var retryOrchestrator = CreateRetryOrchestrator();

            var op = new Get<dynamic> {RetryStrategy = new BestEffortRetryStrategy()};
            var bucketMock = new Mock<BucketBase>("fake", new ClusterContext(), new Mock<IScopeFactory>().Object, retryOrchestrator, new Mock<ILogger>().Object);
            bucketMock.Setup(x => x.SendAsync(op, It.IsAny<CancellationToken>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask);

            var tokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(2500));
            tokenSource.Token.ThrowIfCancellationRequested();

            await retryOrchestrator.RetryAsync(bucketMock.Object, op, tokenSource.Token,
                TimeSpan.FromMilliseconds(2500));

            Assert.Equal(1u, op.Attempts);
        }

        [Theory]
        [ClassData(typeof(NotRetryTestData))]
        public async Task Non_Idempotent_Fails_Without_Retry(object operation, Exception exception)
        {
            await AssertDoesNotRetryAsync((IOperation) operation, exception);
        }

        private async Task AssertDoesNotRetryAsync(IOperation op, Exception exp)
        {
            var retryOrchestrator = CreateRetryOrchestrator();

            var bucketMock = new Mock<BucketBase>("name", new ClusterContext(), new Mock<IScopeFactory>().Object, retryOrchestrator, new Mock<ILogger>().Object);
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

        [Fact(Skip = "Test incomplete")]
        public void Add_DoesNot_Retry_When_KeyExists()
        {
            var op = new Add<dynamic> { RetryStrategy = new BestEffortRetryStrategy() };
        }

        [Theory]
        [InlineData("query-success.json", HttpStatusCode.OK, null)]
        [InlineData("query-facets.json", HttpStatusCode.OK, null)]
        [InlineData("query-error.json", HttpStatusCode.OK, null)]
        public async Task Test_Search(string file, HttpStatusCode httpStatusCode, Type errorType)
        {
            var retryOrchestrator = CreateRetryOrchestrator();

            using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Search\" + file);
            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var responses = GetResponses(20, buffer, httpStatusCode);
            var client = MockedHttpClients.SearchClient(responses);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(1000);

            var searchRequest = new SearchRequest
            {
                Token = cts.Token,
                Timeout = TimeSpan.FromMilliseconds(1000),
                Options = new SearchOptions
                {
                    Token = cts.Token
                }
            };

            async Task<ISearchResult> Func()
            {
                var client1 = client;
                return await client1.QueryAsync(searchRequest, cts.Token);
            }

            await AssertThrowsIfExpectedAsync(errorType, () => retryOrchestrator.RetryAsync(Func, searchRequest));
        }

        [Theory]
        [InlineData("200-success.json", HttpStatusCode.OK, null)]
        [InlineData("404-designdoc-notfound.json", HttpStatusCode.NotFound, typeof(ViewNotFoundException))]
        [InlineData("404-view-notfound.json", HttpStatusCode.NotFound, typeof(ViewNotFoundException))]
        public async Task Test_Views(string file, HttpStatusCode httpStatusCode, Type errorType)
        {
            var retryOrchestrator = CreateRetryOrchestrator();

            using (var response = ResourceHelper.ReadResourceAsStream(@"Documents\Views\" + file))
            {
                var buffer = new byte[response.Length];
                response.Read(buffer, 0, buffer.Length);

                var responses = GetResponses(20, buffer, httpStatusCode);
                var client = MockedHttpClients.ViewClient(responses);

                using var cts = new CancellationTokenSource();
                cts.CancelAfter(1000);

                var viewQuery = new ViewQuery("default", "beers", "brewery_beers")
                {
                    Token = cts.Token
                };

                async Task<IViewResult<dynamic, dynamic>> Func()
                {
                    var client1 = client;
                    return await client1.ExecuteAsync<dynamic, dynamic>(viewQuery);
                }

                await AssertThrowsIfExpectedAsync(errorType, () => retryOrchestrator.RetryAsync(Func, viewQuery));
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
            var retryOrchestrator = CreateRetryOrchestrator();

            using (var response = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\" + file))
            {
                var buffer = new byte[response.Length];
                response.Read(buffer, 0, buffer.Length);

                var responses = GetResponses(20, buffer, httpStatusCode);
                var client = MockedHttpClients.AnalyticsClient(responses);

                using var cts = new CancellationTokenSource();
                cts.CancelAfter(1000);

                var options = new AnalyticsOptions().
                    Timeout(TimeSpan.FromSeconds(1000)).CancellationToken(cts.Token).Readonly(readOnly);

                var query = new AnalyticsRequest("SELECT * FROM `bar`;")
                {
                    ClientContextId = options.ClientContextIdValue,
                    NamedParameters = options.NamedParameters,
                    PositionalArguments = options.PositionalParameters,
                    ReadOnly = options.ReadonlyValue,
                    Idempotent = options.ReadonlyValue
                };
                query.WithTimeout(options.TimeoutValue);
                query.Priority(options.PriorityValue);
                query.Token = options.Token;

                async Task<IAnalyticsResult<dynamic>> Send()
                {
                    var client1 = client;
                    return await client1.QueryAsync<dynamic>(query, options.Token);
                }

                await AssertThrowsIfExpectedAsync(errorType, () => retryOrchestrator.RetryAsync(Send, query));
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
            var retryOrchestrator = CreateRetryOrchestrator();

            using (var response = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\" + file))
            {
                var buffer = new byte[response.Length];
                response.Read(buffer, 0, buffer.Length);

                var responses = GetResponses(20, buffer, httpStatusCode);
                var client = MockedHttpClients.QueryClient(responses);

                using var cts = new CancellationTokenSource();
                cts.CancelAfter(100000);

                var queryOptions = new QueryOptions().
                    ReadOnly(readOnly).
                    CancellationToken(cts.Token).
                    Timeout(TimeSpan.FromMilliseconds(100000));

                var request = new QueryRequest
                {
                    Options = queryOptions,
                    Statement = "SELECT * FROM `default`",
                    Timeout = TimeSpan.FromMilliseconds(100),
                    Token = cts.Token
                };

                async Task<IQueryResult<dynamic>> Func()
                {
                    var client1 = client;
                    return await client1.QueryAsync<dynamic>(request.Statement, queryOptions);
                }

                var e = await AssertThrowsIfExpectedAsync(errorType, () => retryOrchestrator.RetryAsync(Func, request));

                if (e != null)
                {
                    // Did throw exception, as expected, now validate the exception

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

        private static async Task<Exception> AssertThrowsIfExpectedAsync(Type exceptionType, Func<Task> action)
        {
            if (exceptionType != null)
            {
                return await Assert.ThrowsAsync(exceptionType, action);
            }
            else
            {
                await action();

                return null;
            }
        }

        private static RetryOrchestrator CreateRetryOrchestrator() =>
            new RetryOrchestrator(new Mock<ILogger<RetryOrchestrator>>().Object);
    }
}
