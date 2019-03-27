using System;
using System.Buffers;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class MemoryPoolExtensionsTests
    {
        #region RentAndSlice

        [Theory]
        [InlineData(1)]
        [InlineData(32)]
        [InlineData(16383)]
        [InlineData(16384)]
        public void RentAndSlice_RequestLength_ReturnsExactlyThatLength(int length)
        {
            using (var memory = MemoryPool<byte>.Shared.RentAndSlice(length))
            {
                Assert.Equal(length, memory.Memory.Length);
            }
        }

        #endregion
    }
}
