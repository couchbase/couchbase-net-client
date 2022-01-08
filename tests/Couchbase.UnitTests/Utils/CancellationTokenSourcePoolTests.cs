using System;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class CancellationTokenSourcePoolTests
    {
        [Fact]
        public void RentAndReturn_MixedCancellations_NoErrors()
        {
            // Arrange

            using var pool = new CancellationTokenSourcePool();

            // Act/Assert

            var rand = new Random(123456);
            for (var i = 0; i < 1000; i++)
            {
                var cts = pool.Rent();

                // New tokens should never be marked for cancellation
                Assert.False(cts.IsCancellationRequested);

                if (rand.Next(0, 10) < 3)
                {
                    // Cancel 30% of the requested tokens
                    cts.Cancel();
                }

                pool.Return(cts);
            }
        }
    }
}
