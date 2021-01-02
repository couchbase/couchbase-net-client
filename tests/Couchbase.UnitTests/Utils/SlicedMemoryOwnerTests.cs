using System;
using System.Buffers;
using System.Linq;
using Couchbase.UnitTests.Helpers;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class SlicedMemoryOwnerTests
    {
        #region ctor1

        [Fact]
        public void ctor1_NullMemoryOwner_ArgumentNullException()
        {
            // Act/Assert

            Assert.Throws<ArgumentNullException>(() => new SlicedMemoryOwner<byte>(null, 0));
        }

        [Theory]
        [InlineData(-100)]
        [InlineData(-1)]
        [InlineData(33)]
        [InlineData(100)]
        public void ctor1_StartOutsideRange_ArgumentOutOfRangeException(int start)
        {
            // Act/Assert
            using (var memory = MemoryPool<byte>.Shared.Rent(32))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new SlicedMemoryOwner<byte>(memory, start));
            }
        }

        [Theory]
        [InlineData(0, 32)]
        [InlineData(10, 22)]
        [InlineData(31, 1)]
        public void ctor1_StartWithinRange_SliceUntilEnd(int start, int expectedLength)
        {
            // Act/Assert
            using (var memory = new SlicedMemoryOwner<byte>(MemoryPool<byte>.Shared.Rent(32), start))
            {
                Assert.Equal(expectedLength, memory.Memory.Length);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(10)]
        [InlineData(31)]
        public void ctor1_StartWithinRange_SliceStart(int start)
        {
            // Arrange

            var bytes = Enumerable.Range(0, 32).Select(p => (byte) p).ToArray();
            var fakeMemoryOwner = new FakeMemoryOwner<byte>(bytes);

            // Act

            var slicedOwner = new SlicedMemoryOwner<byte>(fakeMemoryOwner, start);

            // Assert

            Assert.Equal(bytes[start], slicedOwner.Memory.Span[0]);
        }

        #endregion

        #region ctor2

        [Fact]
        public void ctor2_NullMemoryOwner_ArgumentNullException()
        {
            // Act/Assert

            Assert.Throws<ArgumentNullException>(() => new SlicedMemoryOwner<byte>(null, 0, 1));
        }

        [Fact]
        public void ctor2_NegativeStart_ArgumentOutOfRangeException()
        {
            // Act/Assert
            using (var memory = MemoryPool<byte>.Shared.Rent(32))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new SlicedMemoryOwner<byte>(memory, -1, 10));
            }
        }

        [Theory]
        [InlineData(-100)]
        [InlineData(-1)]
        [InlineData(32)]
        [InlineData(100)]
        public void ctor2_StartOutsideRange_ArgumentOutOfRangeException(int start)
        {
            // Act/Assert
            using (var memory = MemoryPool<byte>.Shared.Rent(32))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new SlicedMemoryOwner<byte>(memory, start, 1));
            }
        }

        [Theory]
        [InlineData(0, 32)]
        [InlineData(10, 22)]
        [InlineData(31, 1)]
        public void ctor2_StartWithinRange_SliceLength(int start, int length)
        {
            // Act/Assert
            using (var memory = new SlicedMemoryOwner<byte>(MemoryPool<byte>.Shared.Rent(32), start, length))
            {
                Assert.Equal(length, memory.Memory.Length);
            }
        }

        [Theory]
        [InlineData(0, 32)]
        [InlineData(10, 22)]
        [InlineData(31, 1)]
        public void ctor2_StartWithinRange_SliceStart(int start, int length)
        {
            // Arrange

            var bytes = Enumerable.Range(0, 32).Select(p => (byte) p).ToArray();
            var fakeMemoryOwner = new FakeMemoryOwner<byte>(bytes);

            // Act

            var slicedOwner = new SlicedMemoryOwner<byte>(fakeMemoryOwner, start, length);

            // Assert

            Assert.Equal(bytes[start], slicedOwner.Memory.Span[0]);
        }

        #endregion

        #region Dispose

        [Fact]
        public void Dispose_DisposesInnerMemory()
        {
            // Arrange

            var bytes = Enumerable.Range(0, 32).Select(p => (byte) p).ToArray();
            var fakeMemoryOwner = new FakeMemoryOwner<byte>(bytes);
            var slicedOwner = new SlicedMemoryOwner<byte>(fakeMemoryOwner, 0);

            // Act

            slicedOwner.Dispose();

            // Assert

            Assert.True(fakeMemoryOwner.Disposed);
        }

        #endregion
    }
}
