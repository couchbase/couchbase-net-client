using System;
using System.Buffers;
using System.Text;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    /// <summary>
    /// Low-level byte-by-byte tests to verify the packet format is correct after the VBucketId fix.
    /// These tests examine the raw packet bytes to ensure the specific bug (30066 = 0x7572) is resolved.
    /// </summary>
    public class PacketFormatVBucketIdTests
    {
        [Fact]
        public void GetErrorMap_PacketBytes_VBucketFieldIsZero()
        {
            // Arrange
            using var builder = new OperationBuilder();
            using var operation = new GetErrorMap
            {
                VBucketId = 30066, // The exact problematic value from the bug report
                Opaque = 0x12345678
            };

            // Act - Build complete packet
            operation.WriteExtras(builder);
            operation.WriteKey(builder);
            operation.WriteBody(builder);

            var header = operation.CreateHeader(DataType.Json);
            builder.WriteHeader(header);

            var packet = builder.GetBuffer().Span;

            // Assert - Examine raw bytes at VBucket position (bytes 6-7)
            Assert.Equal(0x00, packet[6]); // High byte of VBucket should be 0
            Assert.Equal(0x00, packet[7]); // Low byte of VBucket should be 0

            // Verify the problematic bytes 30066 (0x7572) are NOT present
            Assert.False(packet[6] == 0x75 && packet[7] == 0x72,
                "Packet contains the problematic 30066 value (0x7572) in VBucket field");
        }

        [Fact]
        public void GetErrorMap_PacketStructure_MatchesProtocolSpecification()
        {
            // Arrange - Build a complete GetErrorMap packet
            using var builder = new OperationBuilder();
            using var operation = new GetErrorMap
            {
                Opaque = 0xABCDEF01,
                Cas = 0x1122334455667788
            };

            // Act
            operation.WriteExtras(builder);
            operation.WriteKey(builder);
            operation.WriteBody(builder);

            var header = operation.CreateHeader(DataType.Json);
            builder.WriteHeader(header);

            var packet = builder.GetBuffer().Span;

            // Assert - Verify complete packet structure according to memcached protocol
            // Byte 0: Magic
            Assert.Equal((byte)Magic.AltRequest, packet[0]);

            // Byte 1: OpCode
            Assert.Equal((byte)OpCode.GetErrorMap, packet[1]);

            // Bytes 2-3: Key Length (should be 0 for GetErrorMap)
            Assert.Equal(0x02, packet[2]);
            Assert.Equal(0x00, packet[3]);

            // Byte 4: Extras Length (should be 0 for GetErrorMap)
            Assert.Equal(0x00, packet[4]);

            // Byte 5: Data Type
            Assert.Equal((byte)DataType.Json, packet[5]);

            // Bytes 6-7: VBucket (THIS IS THE BUG LOCATION - should be 0)
            Assert.Equal(0x00, packet[6]);
            Assert.Equal(0x00, packet[7]);

            // Bytes 8-11: Body Length (should be 2 for version ushort)
            var bodyLength = ByteConverter.ToInt32(packet.Slice(8));
            Assert.Equal(2, bodyLength);

            // Bytes 12-15: Opaque
            var opaque = ByteConverter.ToUInt32(packet.Slice(12), useNbo:false);
            Assert.Equal(0xABCDEF01u, opaque);

            // Bytes 16-23: CAS
            var cas = ByteConverter.ToUInt64(packet.Slice(16));
            Assert.Equal(0x1122334455667788u, cas);

            // Bytes 24-25: Body content (version = 2)
            var version = ByteConverter.ToInt16(packet.Slice(24));
            Assert.Equal(2, version);
        }

        [Fact]
        public void GetErrorMap_HexDump_DoesNotContain7572()
        {
            // Arrange - Create packet and examine as hex
            using var builder = new OperationBuilder();
            using var operation = new GetErrorMap { Opaque = 0x11111111 };

            // Act
            operation.WriteExtras(builder);
            operation.WriteKey(builder);
            operation.WriteBody(builder);

            var header = operation.CreateHeader(DataType.Json);
            builder.WriteHeader(header);

            var packet = builder.GetBuffer().Span.Slice(0, OperationHeader.Length);

            string hexDump;
#if NET8_0_OR_GREATER
            // Assert - Convert to hex string and verify no 7572 pattern
            hexDump = Convert.ToHexString(packet.ToArray()).ToLower();
#else
            // Assert - Convert to hex string and verify no 7572 pattern
            hexDump = BitConverter.ToString(packet.ToArray()).Replace("-", "");
#endif

            // The VBucket field should be "0000", not "7572"
            var vbucketHex = hexDump.Substring(12, 4); // Bytes 6-7 in hex (positions 12-15)
            Assert.Equal("0000", vbucketHex);
            Assert.NotEqual("7572", vbucketHex);

            // Ensure the problematic pattern doesn't appear anywhere in the header
            Assert.DoesNotContain("7572", hexDump);
        }

        [Fact]
        public void RegularKvOperation_PacketBytes_VBucketFieldHasCorrectValue()
        {
            // Arrange - Test that normal operations still work correctly
            using var builder = new OperationBuilder();

            var header = new OperationRequestHeader
            {
                OpCode = OpCode.Get,
                VBucketId = 1000, // Should be written to packet
                Opaque = 0xDEADBEEF,
                DataType = DataType.Json
            };

            // Act
            builder.WriteHeader(header);

            var packet = builder.GetBuffer().Span;

            // Assert - VBucket should contain 1000 (0x03E8)
            Assert.Equal(0x03, packet[6]); // High byte of 1000
            Assert.Equal(0xE8, packet[7]); // Low byte of 1000

            var vbucketValue = ByteConverter.ToInt16(packet.Slice(6));
            Assert.Equal(1000, vbucketValue);
        }

        [Fact]
        public void CornerCase_MaxVBucketId_WrittenCorrectly()
        {
            // Arrange - Test edge case with maximum VBucket value
            using var builder = new OperationBuilder();

            var header = new OperationRequestHeader
            {
                OpCode = OpCode.Get,
                VBucketId = short.MaxValue, // 32767 = 0x7FFF
                Opaque = 0x11111111,
                DataType = DataType.Json
            };

            // Act
            builder.WriteHeader(header);

            var packet = builder.GetBuffer().Span;

            // Assert
            Assert.Equal(0x7F, packet[6]); // High byte
            Assert.Equal(0xFF, packet[7]); // Low byte

            var vbucketValue = ByteConverter.ToInt16(packet.Slice(6));
            Assert.Equal(short.MaxValue, vbucketValue);
        }

        [Fact]
        public void GetErrorMap_EndToEndPacketCreation_VBucketIsZero()
        {
            // Arrange - Full end-to-end test mimicking real usage
            var arrayPool = ArrayPool<byte>.Create();
            using var builder = new OperationBuilder();
            using var operation = new GetErrorMap
            {
                VBucketId = 999, // Should be completely ignored
                Opaque = 0xCAFEBABE
            };

            // Act - Full packet creation process
            operation.WriteExtras(builder);
            operation.WriteKey(builder);
            operation.WriteBody(builder);

            var header = operation.CreateHeader(DataType.Json);
            builder.WriteHeader(header);

            var completePacket = builder.GetBuffer().Span;

            // Assert - Verify the complete packet is correct
            // Header portion
            Assert.Equal((byte)Magic.AltRequest, completePacket[0]);
            Assert.Equal((byte)OpCode.GetErrorMap, completePacket[1]);
            Assert.Equal(0x00, completePacket[6]); // VBucket high byte = 0
            Assert.Equal(0x00, completePacket[7]); // VBucket low byte = 0

            // Body portion (should contain version = 2)
            var bodyStart = OperationHeader.Length;
            var version = ByteConverter.ToInt16(completePacket.Slice(bodyStart));
            Assert.Equal(2, version);

            // Verify total packet length
            var expectedLength = OperationHeader.Length + 2; // Header + version short
            Assert.Equal(expectedLength, completePacket.Length);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(30066)] // The specific problematic value
        [InlineData(32000)]
        public void GetErrorMap_IgnoresAllVBucketIdValues(short vbucketId)
        {
            // Arrange
            using var builder = new OperationBuilder();
            using var operation = new GetErrorMap { VBucketId = vbucketId };

            // Act
            operation.WriteExtras(builder);
            operation.WriteKey(builder);
            operation.WriteBody(builder);

            var header = operation.CreateHeader(DataType.Json);
            builder.WriteHeader(header);

            var packet = builder.GetBuffer().Span;

            // Assert - VBucket should always be 0x0000 regardless of input
            Assert.Equal(0x00, packet[6]);
            Assert.Equal(0x00, packet[7]);
        }
    }
}
