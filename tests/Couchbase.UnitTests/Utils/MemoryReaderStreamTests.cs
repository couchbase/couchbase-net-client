using System;
using System.IO;
using System.Linq;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class MemoryReaderStreamTests
    {
        private static readonly byte[] TestBytes = Enumerable.Range(0, 100)
            .Select(p => (byte) p)
            .ToArray();

        #region Seek

        [Theory]
        [InlineData(0, SeekOrigin.Begin, 0)]
        [InlineData(5, SeekOrigin.Begin, 5)]
        [InlineData(0, SeekOrigin.Current, 0)]
        [InlineData(5, SeekOrigin.Current, 5)]
        [InlineData(0, SeekOrigin.End, 100)]
        [InlineData(-5, SeekOrigin.End, 95)]
        [InlineData(5, SeekOrigin.End, 105)]
        public void Seek_Offset_ExpectedPosition(int offset, SeekOrigin origin, int expectedPosition)
        {
            // Arrange

            var stream = new MemoryReaderStream(TestBytes.AsMemory());

            // Act

            stream.Seek(offset, origin);

            // Assert

            Assert.Equal(expectedPosition, stream.Position);
        }

        [Theory]
        [InlineData(-1, SeekOrigin.Begin)]
        [InlineData(-1, SeekOrigin.Current)]
        [InlineData(-101, SeekOrigin.End)]
        public void Seek_BeforeBeginning_ArgumentOutOfRange(int offset, SeekOrigin origin)
        {
            // Arrange

            var stream = new MemoryReaderStream(TestBytes.AsMemory());

            // Act/Assert

            Assert.Throws<ArgumentOutOfRangeException>(() => stream.Seek(offset, origin));
        }

        [Theory]
        [InlineData(100, SeekOrigin.Begin)]
        [InlineData(200, SeekOrigin.Begin)]
        [InlineData(100, SeekOrigin.Current)]
        [InlineData(200, SeekOrigin.Current)]
        [InlineData(0, SeekOrigin.End)]
        [InlineData(100, SeekOrigin.End)]
        public void Seek_AfterEnd_EmptyRead(int offset, SeekOrigin origin)
        {
            // Arrange

            var stream = new MemoryReaderStream(TestBytes.AsMemory());

            // Act

            stream.Seek(offset, origin);


            var actual = new byte[1];
            var readBytes = stream.Read(actual, 0, actual.Length);

            // Assert

            Assert.Equal(0, readBytes);
        }

        [Fact]
        public void Seek_SuccessiveCurrent_CorrectPosition()
        {
            // Arrange

            var stream = new MemoryReaderStream(TestBytes.AsMemory());

            // Act

            stream.Seek(20, SeekOrigin.Current);
            stream.Seek(20, SeekOrigin.Current);
            stream.Seek(-5, SeekOrigin.Current);

            // Assert

            Assert.Equal(35, stream.Position);
        }

        #endregion

        #region Read

        [Theory]
        [InlineData(0, new byte[] {0, 1, 2, 3, 4, 5, 6, 7})]
        [InlineData(5, new byte[] {5, 6, 7, 8, 9, 10, 11, 12})]
        [InlineData(98, new byte[] {98, 99})]
        [InlineData(100, new byte[] {})]
        [InlineData(102, new byte[] {})]
        public void Read_ByteArray_ExpectedBytes(int position, byte[] expectedBytes)
        {
            // Arrange

            var stream = new MemoryReaderStream(TestBytes.AsMemory())
            {
                Position = position
            };

            // Act

            var actual = new byte[8];
            var readBytes = stream.Read(actual, 0, 8);

            // Assert

            Assert.Equal(expectedBytes.Length, readBytes);
            Assert.Equal(expectedBytes, actual.Take(readBytes));
        }

        [Theory]
        [InlineData(0, 5, 5)]
        [InlineData(50, 10, 60)]
        [InlineData(98, 5, 100)]
        public void Read_ByteArray_AdvancesPosition(int position, int length, int expectedPosition)
        {
            // Arrange

            var stream = new MemoryReaderStream(TestBytes.AsMemory())
            {
                Position = position
            };

            // Act

            var actual = new byte[length];
            stream.Read(actual, 0, length);

            // Assert

            Assert.Equal(expectedPosition, stream.Position);
        }

#if NETCOREAPP2_1

        [Theory]
        [InlineData(0, new byte[] {0, 1, 2, 3, 4, 5, 6, 7})]
        [InlineData(5, new byte[] {5, 6, 7, 8, 9, 10, 11, 12})]
        [InlineData(98, new byte[] {98, 99})]
        [InlineData(100, new byte[] {})]
        [InlineData(102, new byte[] {})]
        public void Read_Span_ExpectedBytes(int position, byte[] expectedBytes)
        {
            // Arrange

            var stream = new MemoryReaderStream(TestBytes.AsMemory())
            {
                Position = position
            };

            // Act

            var actual = new byte[8];
            var readBytes = stream.Read(actual.AsSpan());

            // Assert

            Assert.Equal(expectedBytes.Length, readBytes);
            Assert.Equal(expectedBytes, actual.Take(readBytes));
        }

        [Theory]
        [InlineData(0, 5, 5)]
        [InlineData(50, 10, 60)]
        [InlineData(98, 5, 100)]
        public void Read_Span_AdvancesPosition(int position, int length, int expectedPosition)
        {
            // Arrange

            var stream = new MemoryReaderStream(TestBytes.AsMemory())
            {
                Position = position
            };

            // Act

            var actual = new byte[length];
            stream.Read(actual.AsSpan());

            // Assert

            Assert.Equal(expectedPosition, stream.Position);
        }

#endif

        #endregion
    }
}
