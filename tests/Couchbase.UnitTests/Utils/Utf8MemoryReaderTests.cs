using System;
using System.Text;
using Couchbase.Test.Common.Utils;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class Utf8MemoryReaderTests
    {
        [Fact]
        public void Read_EmptyBuffer_ReturnsZero()
        {
            // Arrange

            var reader = new Utf8MemoryReader();
            reader.SetMemory((byte[])[0]);

            var buffer = new char[10];

            // Act

            var readChars = reader.Read(buffer, 0, 0);

            // Assert

            Assert.Equal(0, readChars);
        }

        [Fact]
        public void Read_EmptySource_ReturnsZero()
        {
            // Arrange

            var reader = new Utf8MemoryReader();
            reader.SetMemory((byte[])[]);

            var buffer = new char[10];

            // Act

            var readChars = reader.Read(buffer, 0, 10);

            // Assert

            Assert.Equal(0, readChars);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Read_OffsetAndCount_Respected(bool splitBuffer)
        {
            // Arrange

            var reader = new Utf8MemoryReader();

            var source = "ABCDE"u8.ToArray();
            if (splitBuffer)
            {
                reader.SetSequence(SequenceHelpers.CreateSequenceFromSplitIndex(source, 2));
            }
            else
            {
                reader.SetMemory(source);
            }

            var buffer = new char[10];

            // Act

            var readChars = reader.Read(buffer, 3, 4);

            // Assert

            Assert.Equal(4, readChars);
            Assert.Equal("\0\0\0ABCD\0\0\0", buffer);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Read_MultipleReads_Streams(bool splitBuffer)
        {
            // Arrange

            var reader = new Utf8MemoryReader();

            var source = "ABCDEFG"u8.ToArray();
            if (splitBuffer)
            {
                reader.SetSequence(SequenceHelpers.CreateSequenceFromSplitIndex(source, 5));
            }
            else
            {
                reader.SetMemory(source);
            }

            var buffer = new char[10];

            // Act 1

            var readChars1 = reader.Read(buffer, 0, 4);

            // Assert 1

            Assert.Equal(4, readChars1);
            Assert.Equal("ABCD".PadRight(buffer.Length, '\0'), buffer);

            // Act 2

            var readChars2 = reader.Read(buffer, readChars1, buffer.Length - readChars1);

            // Assert 1

            Assert.Equal(3, readChars2);
            Assert.Equal(Encoding.UTF8.GetString(source).PadRight(buffer.Length, '\0'), buffer);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Read_SplitSurrogatePair_ReturnedInNextRead(bool splitBuffer)
        {
            // Arrange

            var reader = new Utf8MemoryReader();

            // UTF-8 encoding of "🇺🇸🇨🇦" is 16 bytes, consisting of 2 surrogate pairs each, so the total string is 20 bytes
            var source = "AB🇺🇸🇨🇦CD"u8.ToArray();
            if (splitBuffer)
            {
                reader.SetSequence(SequenceHelpers.CreateSequenceFromSplitIndex(source, 5));
            }
            else
            {
                reader.SetMemory(source);
            }

            var buffer = new char[Encoding.UTF8.GetCharCount(source)];

            // Act 1

            var readChars1 = reader.Read(buffer, 0, 5);

            // Assert 1

            Assert.Equal(5, readChars1); // Reads the 🇺 surrogate pair and half of the 🇸 pair
            Assert.Equal("AB🇺\ud83c".PadRight(buffer.Length, '\0'), buffer);

            // Act 2

            var readChars2 = reader.Read(buffer, readChars1, buffer.Length - readChars1);

            // Assert 1

            Assert.Equal(buffer.Length - readChars1, readChars2); // Reads the remainder of the 🇸 surrogate pair and the rest of the string
            Assert.Equal(Encoding.UTF8.GetChars(source), buffer);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Read_SplitSurrogatePair_DoesNotOverflowBuffer(bool splitBuffer)
        {
            // https://issues.couchbase.com/browse/NCBC-3842

            // Arrange

            var reader = new Utf8MemoryReader();

            // UTF-8 encoding of "🇺🇸🇨🇦" is 16 bytes, consisting of 2 surrogate pairs each, so the total string is 20 bytes
            var source = "AB🇺🇸🇨🇦CD"u8.ToArray();
            if (splitBuffer)
            {
                reader.SetSequence(SequenceHelpers.CreateSequenceFromSplitIndex(source, 5));
            }
            else
            {
                reader.SetMemory(source);
            }

            var buffer = new char[Encoding.UTF8.GetCharCount(source)];

            var readChars1 = reader.Read(buffer, 0, 5);
            Assert.Equal(5, readChars1); // Reads the 🇺 surrogate pair and half of the 🇸 pair

            buffer.AsSpan().Fill('\0'); // Reset the buffer to nulls

            // Act

            var readChars2 = reader.Read(buffer, 0, 3);

            // Assert

            Assert.Equal(3, readChars2);
            Assert.Equal(new string('\0', buffer.Length - 3), buffer.AsSpan(3).ToString()); // Ensure the rest of the buffer is still nulls
        }
    }
}
