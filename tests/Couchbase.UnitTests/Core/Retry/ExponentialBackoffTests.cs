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
                var delay = backoff.CalculateBackoff(mockOp.Object);
                Assert.Equal(data.Key, mockOp.Object.Attempts);
                Assert.Equal(TimeSpan.FromMilliseconds(data.Value), delay);
            }
        }
    }
}
