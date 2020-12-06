using System;
using Couchbase.Core.IO.Operations;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    public class FlagsTests
    {
        #region Read

        [Fact]
        public void Read_ShortBuffer_ThrowArgumentException()
        {
            // Act/Assert

            Assert.Throws<ArgumentException>(() => Flags.Read(new byte[3]));
        }

        [Theory]
        [InlineData(new byte[] { 0x02, 0x00, 0x00, 0x01 }, DataFormat.Json, Couchbase.Core.IO.Operations.Compression.None, TypeCode.Object)]
        [InlineData(new byte[] { 0x01, 0x00, 0x00, 0x12 }, DataFormat.Private, Couchbase.Core.IO.Operations.Compression.None, TypeCode.String)]
        public void Read_ValidBuffer_ExpectedResult(byte[] input, DataFormat expectedDataFormat,
            Couchbase.Core.IO.Operations.Compression expectedCompression, TypeCode expectedTypeCode)
        {
            // Act

            var result = Flags.Read(input);

            // Assert

            Assert.Equal(expectedDataFormat, result.DataFormat);
            Assert.Equal(expectedCompression, result.Compression);
            Assert.Equal(expectedTypeCode, result.TypeCode);
        }

        #endregion

        #region Wryte

        [Fact]
        public void Write_ShortBuffer_ThrowArgumentException()
        {
            // Arrange

            var flags = new Flags();

            // Act/Assert

            Assert.Throws<ArgumentException>(() => flags.Write(new byte[3]));
        }

        [Theory]
        [InlineData(new byte[] { 0x02, 0x00, 0x00, 0x01 }, DataFormat.Json, Couchbase.Core.IO.Operations.Compression.None, TypeCode.Object)]
        [InlineData(new byte[] { 0x01, 0x00, 0x00, 0x12 }, DataFormat.Private, Couchbase.Core.IO.Operations.Compression.None, TypeCode.String)]
        public void Write_ValidBuffer_ExpectedResult(byte[] expectedOutput, DataFormat dataFormat,
            Couchbase.Core.IO.Operations.Compression compression, TypeCode typeCode)
        {
            // Arrange

            var flags = new Flags
            {
                DataFormat = dataFormat,
                Compression = compression,
                TypeCode = typeCode
            };

            var buffer = new byte[] { 0xff, 0xff, 0xff, 0xff }; // Fill with bad data to make sure we overwrite

            // Act

            flags.Write(buffer);

            // Assert

            Assert.Equal(expectedOutput, buffer);
        }

        #endregion
    }
}
