using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;

using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
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
    }
}
