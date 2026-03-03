using System;
using System.Threading;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class CancellationTokenPairSourcePoolTests
    {
        [Fact]
        public void RentAndReturn_MixedCancellations_NoErrors()
        {
            // Arrange
            using var pool = new CancellationTokenPairSourcePool();

            // Act/Assert
            var rand = new Random(123456);
            for (var i = 0; i < 1000; i++)
            {
                using var externalCts = new CancellationTokenSource();
                using (var wrapper = pool.Rent(TimeSpan.FromHours(1), externalCts.Token))
                {
                    // New tokens should never be marked for cancellation
                    Assert.False(wrapper.Token.IsCancellationRequested);

                    // Randomly choose a cancellation type (0=None, 1=Internal, 2=External)
                    var choice = rand.Next(0, 3);
                    if (choice == 1)
                    {
                        // Internal cancellation (e.g. timeout)
                        var sourceField = typeof(CancellationTokenPairSourceWrapper).GetField("_source", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var source = (CancellationTokenPairSource)sourceField.GetValue(wrapper);
                        source.Cancel();
                    }
                    else if (choice == 2)
                    {
                        // External cancellation (e.g. user requested)
                        externalCts.Cancel();
                    }

                    if (choice != 0)
                    {
                        Assert.True(wrapper.Token.IsCancellationRequested);
                    }
                }
            }
        }

        [Fact]
        public void Rent_After_Return_ObjectIsReusableAndNotDisposed()
        {
            // Regression test for NCBC-4116
            // This verifies that an object returned to the pool is actually reusable.
            // By using the 'using' pattern with our new wrapper struct, the Return
            // happens automatically and safely.

            // Arrange
            using var pool = new CancellationTokenPairSourcePool();

            using (var wrapper1 = pool.Rent(TimeSpan.FromSeconds(10), default))
            {
                // wrapper1.Token is active
            } // wrapper1.Dispose() returns it to the pool

            // Act - Rent again
            using (var wrapper2 = pool.Rent(TimeSpan.FromSeconds(10), default))
            {
                // Assert
                // Accessing the object should NOT throw ObjectDisposedException.
                wrapper2.Token.WaitHandle.WaitOne(0); // Should not throw
            }
        }

        [Fact]
        public void Rent_WithCustomTimeProvider_ReturnsNonPooledWrapper()
        {
            // Arrange
            using var pool = new CancellationTokenPairSourcePool();
            var timeProvider = new FakeTimeProvider();

            // Act
            using (var wrapper = pool.Rent(timeProvider, TimeSpan.FromSeconds(10), default))
            {
                // Assert
                Assert.False(wrapper.Token.IsCancellationRequested);
            } // Should call Dispose() on the CTS, not Return() to pool
        }

        [Fact]
        public void Wrapper_Properties_MatchSource()
        {
            // Arrange
            using var pool = new CancellationTokenPairSourcePool();
            var cts = new CancellationTokenSource();
            var externalToken = cts.Token;

            // Act
            using (var wrapper = pool.Rent(TimeSpan.FromSeconds(10), externalToken))
            {
                // Assert
                Assert.Equal(externalToken, wrapper.ExternalToken);
                Assert.NotEqual(CancellationToken.None, wrapper.Token);
                Assert.Equal(wrapper.Token, wrapper.TokenPair.Token);
                Assert.Equal(wrapper.ExternalToken, wrapper.TokenPair.ExternalToken);
                Assert.False(wrapper.IsExternalCancellation);
                Assert.False(wrapper.IsInternalCancellation);
            }
        }
    }
}
