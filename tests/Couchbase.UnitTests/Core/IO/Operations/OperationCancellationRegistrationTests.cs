using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    public class OperationCancellationRegistrationTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InternalCancellation_ReadOnlyOperation_Cancels(bool isSent)
        {
            // Arrange

            var operation = new Get<dynamic>
            {
                IsSent = isSent
            };

            using var cts = new CancellationTokenSource(100);
            using var tokenPair = CancellationTokenPairSource.FromInternalToken(cts.Token);

            // Act

            using var registration = new OperationCancellationRegistration(operation, tokenPair.TokenPair);

            // Assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.Completed.AsTask());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ExternalCancellation_ReadOnlyOperation_Cancels(bool isSent)
        {
            // Arrange

            var operation = new Get<dynamic>
            {
                IsSent = isSent
            };

            using var cts = new CancellationTokenSource(100);
            using var tokenPair = CancellationTokenPairSource.FromExternalToken(cts.Token);

            // Act

            using var registration = new OperationCancellationRegistration(operation, tokenPair.TokenPair);

            // Assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.Completed.AsTask());
        }

        [Fact]
        public async Task InternalCancellation_MutationOperationNotSent_Cancels()
        {
            // Arrange

            var operation = new Set<dynamic>("fake", "fakeKey")
            {
                IsSent = false
            };

            using var cts = new CancellationTokenSource(100);
            using var tokenPair = CancellationTokenPairSource.FromInternalToken(cts.Token);

            // Act

            using var registration = new OperationCancellationRegistration(operation, tokenPair.TokenPair);

            // Assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.Completed.AsTask());
        }

        [Fact]
        public async Task ExternalCancellation_MutationOperationNotSent_Cancels()
        {
            // Arrange

            var operation = new Set<dynamic>("fake", "fakeKey")
            {
                IsSent = false
            };

            using var cts = new CancellationTokenSource(100);
            using var tokenPair = CancellationTokenPairSource.FromExternalToken(cts.Token);

            // Act

            using var registration = new OperationCancellationRegistration(operation, tokenPair.TokenPair);

            // Assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.Completed.AsTask());
        }

        [Fact]
        public async Task InternalCancellation_MutationOperationSent_Cancels()
        {
            // Arrange

            var operation = new Set<dynamic>("fake", "fakeKey")
            {
                IsSent = true
            };

            using var cts = new CancellationTokenSource(100);
            using var tokenPair = CancellationTokenPairSource.FromInternalToken(cts.Token);

            // Act

            using var registration = new OperationCancellationRegistration(operation, tokenPair.TokenPair);

            // Assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.Completed.AsTask());
        }

        [Fact]
        public async Task ExternalCancellation_MutationOperationSent_WaitsForInternalCancellation()
        {
            // Arrange

            var operation = new Set<dynamic>("fake", "fakeKey")
            {
                IsSent = true
            };

            var externalCts = new CancellationTokenSource(100);
            var internalCts = new CancellationTokenSource();
            using var tokenPair = new CancellationTokenPairSource(externalCts.Token, internalCts.Token);

            // Act

            using var registration = new OperationCancellationRegistration(operation, tokenPair.TokenPair);
            tokenPair.Register(() => internalCts.CancelAfter(100));

            // Assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.Completed.AsTask());
            Assert.True(internalCts.IsCancellationRequested);
        }

        [Fact]
        public void Dispose_CancelsRegistration()
        {
            // Arrange

            var operation = new Get<dynamic>();

            var cts = new CancellationTokenSource();
            using var tokenPair = CancellationTokenPairSource.FromInternalToken(cts.Token);

            // Act

            var registration = new OperationCancellationRegistration(operation, tokenPair.TokenPair);
            registration.Dispose();

            // Assert

            cts.Cancel();

            Assert.False(operation.Completed.IsCompleted);
        }
    }
}
