using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;

using Couchbase.Core.Retry;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.Retry
{
    public class ExponentialBackoffTests
    {
        [Fact]
        public void Test_Calculate()
        {
            var testData = new Dictionary<uint, int>
            {
                {0, 1},
                {1, 3},
                {2, 7},
                {3, 15},
                {4, 31},
                {5, 63},
                {6, 127},
                {7, 255},
                {8, 500},
                {9, 500}
            };

            var mockOp = new Mock<IOperation>();
            var backoff = ExponentialBackoff.Create(100, 1, 500);

            foreach (var data in testData)
            {
                mockOp.SetupGet(op => op.Attempts).Returns(data.Key);
                var delay = backoff.CalculateBackoff(mockOp.Object);
                Assert.Equal(TimeSpan.FromMilliseconds(data.Value), delay);
            }
        }

        [Fact]
        public void Is_Not_Stateful()
        {
            // verify that the backoff is based on the number of attempts, not the number of times CalculateBackoff is called.
            var mockOp = new Mock<IOperation>();
            var backoff = ExponentialBackoff.Create(100, 1, 500);

            mockOp.SetupGet(op => op.Attempts).Returns(2);
            for (int i = 0; i < 1000; i++)
            {
                var delay = backoff.CalculateBackoff(mockOp.Object);
                Assert.Equal(TimeSpan.FromMilliseconds(7), delay);
            }

            mockOp.SetupGet(op => op.Attempts).Returns(4);

            for (int i = 0; i < 1000; i++)
            {
                var delay = backoff.CalculateBackoff(mockOp.Object);
                Assert.Equal(TimeSpan.FromMilliseconds(31), delay);
            }
        }

        [Fact]
        public void Does_Not_Throw_On_Max_Retries()
        {
            var mockOp = new Mock<IOperation>();
            var backoff = ExponentialBackoff.Create(10, 1, 500);
            mockOp.SetupGet(op => op.Attempts).Returns(1_000_000);
            var delay = backoff.CalculateBackoff(mockOp.Object);
            Assert.Equal(TimeSpan.FromMilliseconds(500), delay);
        }
    }
}
