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

        #region ToUInt32 / FromUInt32

        [Theory]
        [InlineData(DataFormat.Json, TypeCode.Object)]
        [InlineData(DataFormat.Json, TypeCode.String)]
        [InlineData(DataFormat.Binary, TypeCode.Object)]
        [InlineData(DataFormat.Private, TypeCode.String)]
        [InlineData(DataFormat.String, TypeCode.String)]
        public void ToUInt32_FromUInt32_RoundTrips(DataFormat dataFormat, TypeCode typeCode)
        {
            // Arrange

            var flags = new Flags
            {
                DataFormat = dataFormat,
                Compression = Couchbase.Core.IO.Operations.Compression.None,
                TypeCode = typeCode
            };

            // Act

            var roundTripped = Flags.FromUInt32(flags.ToUInt32());

            // Assert

            Assert.Equal(flags.DataFormat, roundTripped.DataFormat);
            Assert.Equal(flags.Compression, roundTripped.Compression);
            Assert.Equal(flags.TypeCode, roundTripped.TypeCode);
        }

        [Fact]
        public void ToUInt32_UsesNetworkByteOrder_CommonFlagsInTopByte()
        {
            // JSON + Object writes bytes [0x02, 0x00, 0x00, 0x01]; read big-endian => 0x02000001.
            // The common-flags/format nibble must be in the top byte so cross-SDK readers get it
            // via (uf >> 24) & 0xF — matching Java's CodecFlags (format << 24).
            var flags = Flags.JsonCommonFlags;

            Assert.Equal(0x02000001u, flags.ToUInt32());
            Assert.Equal((uint)DataFormat.Json, flags.ToUInt32() >> 24);
            Assert.Equal(DataFormat.Json, Flags.FromUInt32(0x02000001u).DataFormat);
        }

        [Fact]
        public void JsonCommonFlags_IsJsonObject()
        {
            var flags = Flags.JsonCommonFlags;

            Assert.Equal(DataFormat.Json, flags.DataFormat);
            Assert.Equal(Couchbase.Core.IO.Operations.Compression.None, flags.Compression);
            Assert.Equal(TypeCode.Object, flags.TypeCode);
        }

        #endregion
    }
}
