using System;
using System.Buffers.Binary;
using System.Linq;
using Couchbase.Core.IO.Operations;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    public class OperationHeaderTests
    {
        #region Read

        [Fact]
        public void Read_ServerResponse_Reads()
        {
            // Arrange

            byte[] buffer = [
                (byte)Magic.ServerResponse,
                (byte)OpCode.GetErrorMap,
                1, // Key length most significant byte
                25, // Key length least significant byte
                8, // Extras length
                (byte)DataType.Json,
                0x01, // Status most significant byte
                0x99, // Status least significant byte,
                4, // Body length most significant byte
                3,
                2,
                1, // Body length least significant byte
                10, // Opaque most significant byte
                9,
                8,
                7, // Opaque least significant byte
                8, // CAS most significant byte
                7,
                6,
                5,
                4,
                3,
                2,
                1 // CAS least signficant byte
            ];

            // Act

            var header = OperationHeader.Read(buffer);

            // Assert

            Assert.Equal(Magic.ServerResponse, header.Magic);
            Assert.Equal(OpCode.GetErrorMap, header.OpCode);
            Assert.Equal(0, header.FramingExtrasLength);
            Assert.Equal(BinaryPrimitives.ReadInt16BigEndian([1, 25]), header.KeyLength);
            Assert.Equal(8, header.ExtrasLength);
            Assert.Equal(DataType.Json, header.DataType);
            Assert.Equal(ResponseStatus.ClientFailure, header.Status);
            Assert.Equal(BinaryPrimitives.ReadInt32BigEndian([4, 3, 2, 1]), header.BodyLength);
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian([10, 9, 8, 7]), header.Opaque);
            Assert.Equal(BinaryPrimitives.ReadUInt64BigEndian([8, 7, 6, 5, 4, 3, 2, 1]), header.Cas);
        }

        [Fact]
        public void Read_AltResponse_Reads()
        {
            // Arrange

            byte[] buffer = [
                (byte)Magic.AltResponse,
                (byte)OpCode.GetErrorMap,
                3, // Framing extras length
                25, // Key length
                8, // Extras length
                (byte)DataType.Json,
                0x01, // Status most significant byte
                0x99, // Status least significant byte,
                4, // Body length most significant byte
                3,
                2,
                1, // Body length least significant byte
                10, // Opaque most significant byte
                9,
                8,
                7, // Opaque least significant byte
                8, // CAS most significant byte
                7,
                6,
                5,
                4,
                3,
                2,
                1 // CAS least signficant byte
            ];

            // Act

            var header = OperationHeader.Read(buffer);

            // Assert

            Assert.Equal(Magic.AltResponse, header.Magic);
            Assert.Equal(OpCode.GetErrorMap, header.OpCode);
            Assert.Equal(3, header.FramingExtrasLength);
            Assert.Equal(25, header.KeyLength);
            Assert.Equal(8, header.ExtrasLength);
            Assert.Equal(DataType.Json, header.DataType);
            Assert.Equal(ResponseStatus.ClientFailure, header.Status);
            Assert.Equal(BinaryPrimitives.ReadInt32BigEndian([4, 3, 2, 1]), header.BodyLength);
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian([10, 9, 8, 7]), header.Opaque);
            Assert.Equal(BinaryPrimitives.ReadUInt64BigEndian([8, 7, 6, 5, 4, 3, 2, 1]), header.Cas);
        }

        [Fact]
        public void Read_UnknownStatus_UnknownError()
        {
            // Arrange

            byte[] buffer = [
                (byte)Magic.ServerResponse,
                (byte)OpCode.GetErrorMap,
                1, // Key length most significant byte
                25, // Key length least significant byte
                8, // Extras length
                (byte)DataType.Json,
                0xf0, // Status most significant byte
                0xf0, // Status least significant byte,
                4, // Body length most significant byte
                3,
                2,
                1, // Body length least significant byte
                10, // Opaque most significant byte
                9,
                8,
                7, // Opaque least significant byte
                8, // CAS most significant byte
                7,
                6,
                5,
                4,
                3,
                2,
                1 // CAS least signficant byte
            ];

            // Act

            var header = OperationHeader.Read(buffer);

            // Assert

            Assert.Equal(ResponseStatus.UnknownError, header.Status);
        }

        [Fact]
        public void Read_InsufficentLength_None()
        {
            // Arrange

            byte[] buffer = Enumerable.Repeat((byte)0, OperationHeader.Length - 1).ToArray();

            // Act

            var header = OperationHeader.Read(buffer);

            // Assert

            Assert.Equal(ResponseStatus.None, header.Status);
        }

        #endregion

        #region FramingExtrasLength

        [Fact]
        public void FramingExtrasLength_ServerResponse_Zero()
        {
            // Arrange

            var header = new OperationHeader
            {
                Magic = Magic.ServerResponse,
                KeyLength = 250,
            };

            // Act

            var result = header.FramingExtrasLength;

            // Assert

            Assert.Equal(0, result);
        }

        [Fact]
        public void FramingExtrasLength_ServerResponse_SetToZero_Success()
        {
            // Arrange

            var header = new OperationHeader
            {
                Magic = Magic.ServerResponse,
                KeyLength = 250,
            };

            // Act

            header.FramingExtrasLength = 0;
            var result = header.FramingExtrasLength;

            // Assert

            Assert.Equal(0, result);
        }

        [Fact]
        public void FramingExtrasLength_ServerResponse_SetToNonZero_Throws()
        {
            // Arrange

            var header = new OperationHeader
            {
                Magic = Magic.ServerResponse,
                KeyLength = 250,
            };

            // Act/Assert

            Assert.Throws<InvalidOperationException>(() => header.FramingExtrasLength = 0xff);
        }

        [Fact]
        public void FramingExtrasLength_AltResponse_Value()
        {
            // Arrange

            var header = new OperationHeader
            {
                Magic = Magic.AltResponse,
                KeyLength = 250,
                FramingExtrasLength = 0xff
            };

            // Act

            var result = header.FramingExtrasLength;

            // Assert

            Assert.Equal(0xff, result);
        }

        #endregion

        #region KeyLength

        [Fact]
        public void KeyLength_ServerResponse_Value()
        {
            // Arrange

            var header = new OperationHeader
            {
                Magic = Magic.ServerResponse,
                KeyLength = 250
            };

            // Act

            var result = header.KeyLength;

            // Assert

            Assert.Equal(250, result);
        }

        [Fact]
        public void KeyLength_AltResponse_Value()
        {
            // Arrange

            var header = new OperationHeader
            {
                Magic = Magic.AltResponse,
                KeyLength = 250,
                FramingExtrasLength = 0xff
            };

            // Act

            var result = header.KeyLength;

            // Assert

            Assert.Equal(250, result);
        }

        [Fact]
        public void KeyLength_SetToNegative_Throws()
        {
            // Arrange

            var header = new OperationHeader
            {
                Magic = Magic.ServerResponse
            };

            // Act/Assert

            Assert.Throws<ArgumentOutOfRangeException>(() => header.KeyLength = -1);
        }

        [Fact]
        public void KeyLength_SetToGreaterThan250_Throws()
        {
            // Arrange

            var header = new OperationHeader
            {
                Magic = Magic.ServerResponse
            };

            // Act/Assert

            Assert.Throws<ArgumentOutOfRangeException>(() => header.KeyLength = 251);
        }

        #endregion

        #region BodyOffset

        [Fact]
        public void BodyOffset_ServerResponse_Value()
        {
            // Arrange

            var header = new OperationHeader
            {
                Magic = Magic.ServerResponse,
                KeyLength = 250,
                ExtrasLength = 16
            };

            // Act

            var result = header.BodyOffset;

            // Assert

            Assert.Equal(24 + 250 + 16, result);
        }

        [Fact]
        public void BodyOffset_AltResponse_Value()
        {
            // Arrange

            var header = new OperationHeader
            {
                Magic = Magic.AltResponse,
                KeyLength = 250,
                FramingExtrasLength = 255,
                ExtrasLength = 16
            };

            // Act

            var result = header.BodyOffset;

            // Assert

            Assert.Equal(24 + 250 + 255 + 16, result);
        }

        #endregion
    }
}
