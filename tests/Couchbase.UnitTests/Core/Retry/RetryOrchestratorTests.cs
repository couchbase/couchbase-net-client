using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.CircuitBreakers;
//using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.Exceptions.View;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Retry.Query;
using Couchbase.Core.Retry.Search;
using Couchbase.KeyValue;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.Retry
{
    [Collection("NonParallel")]
    public class RetryOrchestratorTests
    {
        public class RetryTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] {new Get<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new Set<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new ReplicaRead<dynamic>("key", 1) {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new GetL<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new GetL<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new MultiLookup<dynamic>("key", Array.Empty<LookupInSpec>()) {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new Config {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};
                yield return new object[] {new Observe {RetryStrategy = new BestEffortRetryStrategy()}, new NotMyVBucketException()};

                yield return new object[] { new Get<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new TemporaryFailureException() };
                yield return new object[] { new Set<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new TemporaryFailureException() };
                yield return new object[] { new ReplicaRead<dynamic>("key", 1) { RetryStrategy = new BestEffortRetryStrategy() }, new TemporaryFailureException() };
                yield return new object[] { new GetL<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new TemporaryFailureException() };
                yield return new object[] { new GetL<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new TemporaryFailureException() };
                yield return new object[] { new MultiLookup<dynamic>("key", Array.Empty<LookupInSpec>()) { RetryStrategy = new BestEffortRetryStrategy() }, new TemporaryFailureException() };
                yield return new object[] { new Config { RetryStrategy = new BestEffortRetryStrategy() }, new TemporaryFailureException() };
                yield return new object[] { new Observe { RetryStrategy = new BestEffortRetryStrategy() }, new TemporaryFailureException() };

                yield return new object[] { new Get<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new CircuitBreakerException() };
                yield return new object[] { new Set<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new CircuitBreakerException() };
                yield return new object[] { new ReplicaRead<dynamic>("key", 1) { RetryStrategy = new BestEffortRetryStrategy() }, new CircuitBreakerException() };
                yield return new object[] { new GetL<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new CircuitBreakerException() };
                yield return new object[] { new GetL<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new CircuitBreakerException() };
                yield return new object[] { new MultiLookup<dynamic>("key", Array.Empty<LookupInSpec>()) { RetryStrategy = new BestEffortRetryStrategy() }, new CircuitBreakerException() };
                yield return new object[] { new Config { RetryStrategy = new BestEffortRetryStrategy() }, new CircuitBreakerException() };
                yield return new object[] { new Observe { RetryStrategy = new BestEffortRetryStrategy() }, new CircuitBreakerException() };

                yield return new object[] { new Get<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new CollectionNotFoundException() };
                yield return new object[] { new Set<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new CollectionNotFoundException() };
                yield return new object[] { new ReplicaRead<dynamic>("key", 1) { RetryStrategy = new BestEffortRetryStrategy() }, new CollectionNotFoundException() };
                yield return new object[] { new GetL<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new CollectionNotFoundException() };
                yield return new object[] { new GetL<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new CollectionNotFoundException() };
                yield return new object[] { new MultiLookup<dynamic>("key", Array.Empty<LookupInSpec>()) { RetryStrategy = new BestEffortRetryStrategy() }, new CollectionNotFoundException() };
                yield return new object[] { new Config { RetryStrategy = new BestEffortRetryStrategy() }, new CollectionNotFoundException() };
                yield return new object[] { new Observe { RetryStrategy = new BestEffortRetryStrategy() }, new CollectionNotFoundException() };

                yield return new object[] { new Get<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new DocumentLockedException() };
                yield return new object[] { new Set<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DocumentLockedException() };
                yield return new object[] { new ReplicaRead<dynamic>("key", 1) { RetryStrategy = new BestEffortRetryStrategy() }, new DocumentLockedException() };
                yield return new object[] { new GetL<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new DocumentLockedException() };
                yield return new object[] { new GetL<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new DocumentLockedException() };
                yield return new object[] { new MultiLookup<dynamic>("key", Array.Empty<LookupInSpec>()) { RetryStrategy = new BestEffortRetryStrategy() }, new DocumentLockedException() };
                yield return new object[] { new Config { RetryStrategy = new BestEffortRetryStrategy() }, new DocumentLockedException() };
                yield return new object[] { new Observe { RetryStrategy = new BestEffortRetryStrategy() }, new DocumentLockedException() };

                yield return new object[] { new Get<dynamic> { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteInProgressException() };
                yield return new object[] { new Set<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteInProgressException() };
                yield return new object[] { new Add<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteInProgressException() };
                yield return new object[] { new Replace<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteInProgressException() };
                yield return new object[] { new Delete { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteInProgressException() };
                yield return new object[] { new Increment("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteInProgressException() };
                yield return new object[] { new Decrement("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteInProgressException() };
                yield return new object[] { new Append<byte[]>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteInProgressException() };
                yield return new object[] { new Prepend<byte>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteInProgressException() };
                yield return new object[] { new MultiMutation<object>("key", Array.Empty<MutateInSpec>()) { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteInProgressException() };

                yield return new object[] { new Set<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteReCommitInProgressException() };
                yield return new object[] { new Add<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteReCommitInProgressException() };
                yield return new object[] { new Replace<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteReCommitInProgressException() };
                yield return new object[] { new Delete { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteReCommitInProgressException() };
                yield return new object[] { new Increment("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteReCommitInProgressException() };
                yield return new object[] { new Decrement("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteReCommitInProgressException() };
                yield return new object[] { new Append<byte[]>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteReCommitInProgressException() };
                yield return new object[] { new Prepend<byte>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteReCommitInProgressException() };
                yield return new object[] { new MultiMutation<object>("key", Array.Empty<MutateInSpec>()) { RetryStrategy = new BestEffortRetryStrategy() }, new DurableWriteReCommitInProgressException() };
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
                yield return new object[] {new Set<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new Delete {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new Append<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new Prepend<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new MultiMutation<dynamic>("key", Array.Empty<MutateInSpec>()) {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new Unlock {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new Touch {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new GetT<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
                yield return new object[] {new GetL<dynamic> {RetryStrategy = new BestEffortRetryStrategy()}, new SocketClosedException()};
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        [Theory]
        [ClassData(typeof(RetryTestData))]
        public async Task Operation_Succeeds_After_N_Retries_Using_BestEffort_When_TemporaryFailure(
            object operation, Exception exception)
        {
            await AssertRetryThenSuccessAsync((OperationBase)operation, exception).ConfigureAwait(false);
        }

        private async Task AssertRetryThenSuccessAsync(OperationBase op, Exception exp)
        {
            var retryOrchestrator = CreateRetryOrchestrator();

            var bucketMock = new Mock<BucketBase>("fake", new ClusterContext(), new Mock<Couchbase.Core.DI.IScopeFactory>().Object,
                retryOrchestrator, new Mock<ILogger>().Object, new Mock<IRedactor>().Object,
                new Mock<IBootstrapperFactory>().Object, NoopRequestTracer.Instance, new Mock<IOperationConfigurator>().Object,
                new BestEffortRetryStrategy());

                bucketMock.Setup(x => x.SendAsync(op, It.IsAny<CancellationTokenPair>())).Callback((IOperation op1, CancellationTokenPair ct) =>
            {
                if (op1.Completed.IsCompleted)
                    Assert.True(false, "operation result should be reset before retry");

                // complete the operation if circuit breaker is not open (ResponseStatus does not matter for this test)
                if (exp.GetType() != typeof(CircuitBreakerException) || op.Attempts != 1)
                    op.HandleOperationCompleted(AsyncState.BuildErrorResponse(op.Opaque, ResponseStatus.TemporaryFailure));

                if (op1.Attempts == 1)
                {
                    throw exp;
                }
            }).Returns(op.Completed.AsTask());

            using var tokenPair = CancellationTokenPairSource.FromTimeout(TimeSpan.FromMilliseconds(2500));

            try
            {
                await retryOrchestrator.RetryAsync(bucketMock.Object, op, tokenPair.TokenPair).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                Assert.True(false, "Expected operation to succeed after retry");
            }
            Assert.True(op.Attempts > 1);
        }

        [Theory(Skip = "test race condition needs debugging NCBC-2935")]
        [ClassData(typeof(RetryTestData))]
        public async Task Operation_Throws_Timeout_After_N_Retries_Using_BestEffort_When_NotMyVBucket(
            object operation, Exception exception)
        {
            await AssertRetryAsync<UnambiguousTimeoutException>((IOperation)operation, exception).ConfigureAwait(false);
        }

        [Fact]
        public async Task Operation_Throws_UnambiguousTimeout_When_ReadOnly_Op_Sent()
        {
            var operation = new Get<dynamic>
            {
                RetryStrategy = new BestEffortRetryStrategy()
            };

            var exception = new OperationCanceledException();

            await AssertRetryAsync<UnambiguousTimeoutException>(operation, exception, minAttempts: 1)
                .ConfigureAwait(false);
        }

        [Fact(Skip = "inconsistent results in jenkins")]
        public async Task Operation_Throws_OperationCanceledException_After_External_Cancellation()
        {
            var operation = new Get<dynamic>
            {
                RetryStrategy = new BestEffortRetryStrategy()
            };

            var exception = new NotMyVBucketException();

            using var cts = new CancellationTokenSource(5000);

            await AssertRetryAsync<OperationCanceledException>(operation, exception, minAttempts: 1,
                externalCancellationToken: cts.Token).ConfigureAwait(false);
        }

        [Fact]
        public async Task Operation_Throws_UnambiguousTimeout_After_Timeout_When_Mutation_Op_Not_Sent()
        {
            var operation = new Set<dynamic>("fake", "fakeKey")
            {
                RetryStrategy = new BestEffortRetryStrategy(),
                IsSent = false
            };

            var exception = new OperationCanceledException();

            await AssertRetryAsync<UnambiguousTimeoutException>(operation, exception, minAttempts: 1).ConfigureAwait(false);
        }

        [Fact]
        public async Task Operation_Throws_AmbiguousTimeout_After_Timeout_When_Mutation_Op_Sent()
        {
            var operation = new Set<dynamic>("fake", "fakeKey")
            {
                RetryStrategy = new BestEffortRetryStrategy(),
                IsSent = true
            };

            var exception = new OperationCanceledException();

            await AssertRetryAsync<AmbiguousTimeoutException>(operation, exception, minAttempts: 1).ConfigureAwait(false);
        }

        private static async Task AssertRetryAsync<TExpected>(IOperation op, Exception exp, int minAttempts = 2,
            CancellationToken externalCancellationToken = default)
            where TExpected : Exception
        {
            var retryOrchestrator = CreateRetryOrchestrator();

            var bucketMock = new Mock<BucketBase>("fake", new ClusterContext(), new Mock<Couchbase.Core.DI.IScopeFactory>().Object,
                retryOrchestrator, new Mock<ILogger>().Object, new Mock<IRedactor>().Object,
                new Mock<IBootstrapperFactory>().Object, NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object,
                new BestEffortRetryStrategy());

            bucketMock.Setup(x => x.SendAsync(op, It.IsAny<CancellationTokenPair>())).Throws(exp);

            using var tokenPair = CancellationTokenPairSource.FromTimeout(TimeSpan.FromMilliseconds(10000), externalCancellationToken);
            try
            {
                await retryOrchestrator.RetryAsync(bucketMock.Object, op, tokenPair.TokenPair).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Assert.IsAssignableFrom<TExpected>(e);

                Assert.True(op.Attempts >= minAttempts, $"Got {op.Attempts} but expected {minAttempts}");
            }
        }

        [Fact]
        public async Task Operation_Succeeds_Without_Retry()
        {
            var retryOrchestrator = CreateRetryOrchestrator();

            var op = new Get<dynamic> {RetryStrategy = new BestEffortRetryStrategy()};
            var bucketMock = new Mock<BucketBase>("fake", new ClusterContext(), new Mock<Couchbase.Core.DI.IScopeFactory>().Object,
                retryOrchestrator, new Mock<ILogger>().Object, new Mock<IRedactor>().Object,
                new Mock<IBootstrapperFactory>().Object, NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object,
                new BestEffortRetryStrategy());

            bucketMock.Setup(x => x.SendAsync(op, It.IsAny<CancellationTokenPair>()))
                .Returns(Task.CompletedTask);

            using var tokenPair = CancellationTokenPairSource.FromTimeout(TimeSpan.FromMilliseconds(2500));

            await retryOrchestrator.RetryAsync(bucketMock.Object, op, tokenPair.TokenPair).ConfigureAwait(false);

            Assert.Equal(1u, op.Attempts);
        }

        [Theory]
        [ClassData(typeof(NotRetryTestData))]
        public async Task Non_Idempotent_Fails_Without_Retry(object operation, Exception exception)
        {
            await AssertDoesNotRetryAsync((IOperation) operation, exception);
        }

        private static async Task AssertDoesNotRetryAsync(IOperation op, Exception exp)
        {
            var retryOrchestrator = CreateRetryOrchestrator();

            var bucketMock = new Mock<BucketBase>("name", new ClusterContext(), new Mock<Couchbase.Core.DI.IScopeFactory>().Object,
                retryOrchestrator, new Mock<ILogger>().Object, new Mock<IRedactor>().Object,
                new Mock<IBootstrapperFactory>().Object,
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object,
                new BestEffortRetryStrategy())
            {
                CallBase = true
            };
            bucketMock.Setup(x => x.SendAsync(op, It.IsAny<CancellationTokenPair>())).Throws(exp);

            using var tokenPair = CancellationTokenPairSource.FromTimeout(TimeSpan.FromMilliseconds(2500));

            try
            {

                await bucketMock.Object.RetryAsync(op, tokenPair.TokenPair).ConfigureAwait(false);
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
            var op = new Add<dynamic>("fake", "fakeKey") { RetryStrategy = new BestEffortRetryStrategy() };
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

                var statement = "SELECT * FROM `bar`;";
                var options = new AnalyticsOptions();
                options.Timeout(TimeSpan.FromSeconds(1000));
                options.CancellationToken(cts.Token);
                options.Readonly(readOnly);

                async Task<IAnalyticsResult<dynamic>> Send()
                {
                    var client1 = client;
                    return await client1.QueryAsync<dynamic>(statement, options);
                }

                var meter = NoopMeter.Instance;
                await AssertThrowsIfExpectedAsync(errorType, () => retryOrchestrator.RetryAsync(Send, AnalyticsRequest.Create(statement, meter.ValueRecorder("analytics"), options)));
            }
        }

        [Theory]
        //only retry 4040 when enhanced prepared statements is enabled
        [InlineData(@"Documents\Query\Retrys\4040.json", HttpStatusCode.BadRequest, typeof(PreparedStatementException), true, false, false)]
        [InlineData(@"Documents\Query\Retrys\4050.json", HttpStatusCode.BadRequest, typeof(UnambiguousTimeoutException), true, true, false)]
        [InlineData(@"Documents\Query\Retrys\4070.json", HttpStatusCode.BadRequest, typeof(UnambiguousTimeoutException), true, true, false)]
        [InlineData(@"Documents\Query\Retrys\5000.json", HttpStatusCode.BadRequest, typeof(UnambiguousTimeoutException), true, true, false)]
        //only retry 4040 when enhanced prepared statements is enabled
        [InlineData(@"Documents\Query\Retrys\4040.json", HttpStatusCode.BadRequest, typeof(AmbiguousTimeoutException), false, true, true)]
        [InlineData(@"Documents\Query\Retrys\4050.json", HttpStatusCode.BadRequest, typeof(PreparedStatementException), false, false, true)]
        [InlineData(@"Documents\Query\Retrys\4070.json", HttpStatusCode.BadRequest, typeof(PreparedStatementException), false, false, true)]
        [InlineData(@"Documents\Query\Retrys\5000.json", HttpStatusCode.BadRequest, typeof(InternalServerFailureException), false, false, true)]
        [InlineData(@"Documents\Query\Retrys\retry_true.json", HttpStatusCode.BadRequest, typeof(UnambiguousTimeoutException), true, true, true)]
        [InlineData(@"Documents\Query\Retrys\retry_false.json", HttpStatusCode.BadRequest, typeof(DocumentNotFoundException), true, false, true)]
        public async Task Test_Query(string file, HttpStatusCode httpStatusCode, Type errorType, bool readOnly, bool canRetry, bool enableEnhancedPreparedStatements)
        {
            var retryOrchestrator = CreateRetryOrchestrator();

            using (var response = ResourceHelper.ReadResourceAsStream(file))
            {
                var buffer = new byte[response.Length];
                response.Read(buffer, 0, buffer.Length);

                var responses = GetResponses(20, buffer, httpStatusCode);
                var client = MockedHttpClients.QueryClient(responses, enableEnhancedPreparedStatements);

                using var cts = new CancellationTokenSource();
                cts.CancelAfter(100000);

                var queryOptions = new QueryOptions().
                    Readonly(readOnly).
                    CancellationToken(cts.Token).
                    Timeout(TimeSpan.FromMilliseconds(100000));

                var request = new QueryRequest
                {
                    Options = queryOptions,
                    Statement = "SELECT * FROM `default`",
                    Timeout = TimeSpan.FromMilliseconds(500),
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
                        Assert.True(request.Attempts > 0, "Attempts: " + request.Attempts);
                    }
                    else
                    {
                        Assert.True(request.Attempts == 0, "Attempts: " + request.Attempts);
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

        private static RetryOrchestrator CreateRetryOrchestrator()
        {

            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder
                .AddFilter(level => level >= LogLevel.Debug)
            );
            var loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
            loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);
            var logger = loggerFactory.CreateLogger<RetryOrchestrator>();

            var mock = new Mock<RetryOrchestrator>(logger,
                new Mock<IRedactor>().Object)
            {
                CallBase = true
            };

            mock
                .Setup(m => m.RefreshCollectionId(It.IsAny<BucketBase>(), It.IsAny<IOperation>()))
                .ReturnsAsync(false);

            return mock.Object;
        }
    }
}
