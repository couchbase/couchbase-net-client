#if !NET6_0_OR_GREATER

using System;
using System.Runtime.InteropServices;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class OperationResponseMemoryPoolTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(32)]
        [InlineData(65536)]
        [InlineData(OperationResponseArrayPoolTests.LargeResponseSize)]
        public void CanRentAndReturn(int size)
        {
            var owner = OperationResponseMemoryPool.Instance.Rent(size);
            Assert.NotNull(owner);

            try
            {
                Assert.InRange(owner.Memory.Length, size, 32 * 1024 * 1024);
            }
            finally
            {
                owner.Dispose();
            }
        }

        [Fact]
        public void ReusesLargeArrays()
        {
            var owner = OperationResponseMemoryPool.Instance.Rent(OperationResponseArrayPoolTests.LargeResponseSize);
            Assert.True(MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>) owner.Memory, out var arraySegment));
            owner.Dispose();

            // should be the same array, given that priority is given to thread-local storage
            owner = OperationResponseMemoryPool.Instance.Rent(OperationResponseArrayPoolTests.LargeResponseSize);
            try
            {
                Assert.True(MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>) owner.Memory, out var arraySegment2));
                Assert.Same(arraySegment.Array, arraySegment2.Array);
            }
            finally
            {
                owner.Dispose();
            }
        }
    }
}

#endif
