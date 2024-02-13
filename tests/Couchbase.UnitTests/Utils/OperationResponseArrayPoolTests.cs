#if !NET6_0_OR_GREATER

using Couchbase.Core.IO.Operations;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class OperationResponseArrayPoolTests
    {
        public const int LargeResponseSize = 20 * 1024 * 1024 + OperationHeader.Length;

        [Theory]
        [InlineData(1)]
        [InlineData(32)]
        [InlineData(65536)]
        [InlineData(LargeResponseSize)]
        public void CanRentAndReturn(int size)
        {
            var arr = OperationResponseArrayPool.Instance.Rent(size);
            Assert.NotNull(arr);

            try
            {
                Assert.InRange(arr.Length, size, 32 * 1024 * 1024);
            }
            finally
            {
                OperationResponseArrayPool.Instance.Return(arr);
            }
        }

        [Fact]
        public void ReusesLargeArrays()
        {
            var arr = OperationResponseArrayPool.Instance.Rent(LargeResponseSize);
            OperationResponseArrayPool.Instance.Return(arr);

            // should be the same array, given that priority is given to thread-local storage
            var arr2 = OperationResponseArrayPool.Instance.Rent(LargeResponseSize);
            try
            {
                Assert.Same(arr, arr2);
            }
            finally
            {
                OperationResponseArrayPool.Instance.Return(arr2);
            }
        }
    }
}

#endif
