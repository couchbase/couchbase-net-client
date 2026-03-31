using System;
using System.Buffers;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    /// <summary>
    /// Unit tests for OperationBuilder.WriteHeader method, specifically verifying the VBucketId fix.
    /// The bug was that when VBucketId was null, garbage data (0x7572 = 30066) was written to the buffer.
    /// The fix ensures null VBucketId values are written as 0x0000.
    /// </summary>
    public class OperationBuilderVBucketIdTests
    {
        [Fact]
        public void WriteHeader_WithNullVBucketId_WritesZeroToBuffer()
        {
            // Arrange
            using var builder = new OperationBuilder();

            var header = new OperationRequestHeader
            {
                OpCode = OpCode.GetErrorMap,
                VBucketId = null, // This should write 0x0000 to buffer
                Opaque = 0x12345678,
                DataType = DataType.Json
            };

            // Act
            builder.WriteHeader(header);
            var buffer = builder.GetBuffer().Span;

            // Assert - VBucket field (bytes 6-7) should be 0x0000
            Assert.Equal(0x00, buffer[6]); // High byte
            Assert.Equal(0x00, buffer[7]); // Low byte

            // Verify it's not the problematic garbage value 30066 (0x7572)
            Assert.False(buffer[6] == 0x75 && buffer[7] == 0x72,
                "Buffer contains garbage data 30066 (0x7572) in VBucket field");
        }

        [Fact]
        public void WriteHeader_WithValidVBucketId_WritesCorrectValue()
        {
            // Arrange
            using var builder = new OperationBuilder();

            var header = new OperationRequestHeader
            {
                OpCode = OpCode.Get,
                VBucketId = 1024, // Should be written as 0x0400
                Opaque = 0xABCDEF01,
                DataType = DataType.Json
            };

            // Act
            builder.WriteHeader(header);
            var buffer = builder.GetBuffer().Span;

            // Assert - VBucket field should contain 1024
            var vbucketValue = ByteConverter.ToInt16(buffer.Slice(6));
            Assert.Equal(1024, vbucketValue);
            Assert.Equal(0x04, buffer[6]); // High byte of 1024
            Assert.Equal(0x00, buffer[7]); // Low byte of 1024
        }

        [Fact]
        public void WriteHeader_CompareNullVsZeroVBucket_BehavesIdentically()
        {
            // Arrange
            // Header with null VBucketId
            var nullHeader = new OperationRequestHeader
            {
                OpCode = OpCode.GetErrorMap,
                VBucketId = null,
                Opaque = 0x11111111,
                DataType = DataType.Json
            };

            // Header with zero VBucketId
            var zeroHeader = new OperationRequestHeader
            {
                OpCode = OpCode.GetErrorMap,
                VBucketId = 0,
                Opaque = 0x11111111,
                DataType = DataType.Json
            };

            // Act
            using var builder1 = new OperationBuilder();
            builder1.WriteHeader(nullHeader);
            var nullBuffer = builder1.GetBuffer().Span.Slice(0, OperationHeader.Length).ToArray();

            using var builder2 = new OperationBuilder();
            builder2.WriteHeader(zeroHeader);
            var zeroBuffer = builder2.GetBuffer().Span.Slice(0, OperationHeader.Length).ToArray();

            // Assert - Both should produce identical headers
            Assert.Equal(zeroBuffer, nullBuffer);

            // Both should have 0x0000 in VBucket field
            Assert.Equal(0x00, nullBuffer[6]);
            Assert.Equal(0x00, nullBuffer[7]);
            Assert.Equal(0x00, zeroBuffer[6]);
            Assert.Equal(0x00, zeroBuffer[7]);
        }

        [Theory]
        [InlineData(null, 0x00, 0x00)]
        [InlineData(0, 0x00, 0x00)]
        [InlineData(1, 0x00, 0x01)]
        [InlineData(256, 0x01, 0x00)]
        [InlineData(1024, 0x04, 0x00)]
        [InlineData(30066, 0x75, 0x72)] // The problematic value when assigned directly
        public void WriteHeader_VBucketIdValues_WriteCorrectBytes(short vbucketId, byte expectedHighByte, byte expectedLowByte)
        {
            // Arrange
            using var builder = new OperationBuilder();

            var header = new OperationRequestHeader
            {
                OpCode = OpCode.Get,
                VBucketId = vbucketId,
                Opaque = 0xDEADBEEF,
                DataType = DataType.Json
            };

            // Act
            builder.WriteHeader(header);
            var buffer = builder.GetBuffer().Span;

            // Assert
            Assert.Equal(expectedHighByte, buffer[6]);
            Assert.Equal(expectedLowByte, buffer[7]);
        }

        [Fact]
        public void WriteHeader_BufferReuseScenario_NoGarbageData()
        {
            // Arrange - Simulate buffer reuse that could contain garbage data

            // First, write something with a VBucketId that might leave garbage
            using (var builder1 = new OperationBuilder())
            {
                var header1 = new OperationRequestHeader
                {
                    OpCode = OpCode.Get,
                    VBucketId = 30066, // 0x7572 - the problematic value
                    Opaque = 0x11111111
                };

                builder1.WriteHeader(header1);
                var buffer1 = builder1.GetBuffer();
                // Buffer is returned to pool when disposed
            }

            // Now write a header with null VBucketId - should not have garbage
            using var builder2 = new OperationBuilder();
            var header2 = new OperationRequestHeader
            {
                OpCode = OpCode.GetErrorMap,
                VBucketId = null, // Should write 0x0000, not inherit garbage
                Opaque = 0x22222222
            };

            // Act
            builder2.WriteHeader(header2);
            var buffer2 = builder2.GetBuffer().Span;

            // Assert - Should be clean 0x0000, not garbage from previous use
            Assert.Equal(0x00, buffer2[6]);
            Assert.Equal(0x00, buffer2[7]);

            // Verify it's not the previous value
            Assert.False(buffer2[6] == 0x75 && buffer2[7] == 0x72,
                "Buffer contains garbage data from previous operations");
        }

        [Fact]
        public void WriteHeader_HeaderStructure_IsCorrect()
        {
            // Arrange
            using var builder = new OperationBuilder();

            var header = new OperationRequestHeader
            {
                OpCode = OpCode.GetErrorMap,
                VBucketId = null,
                DataType = DataType.Json,
                Opaque = 0xABCDEF01,
                Cas = 0x1122334455667788
            };

            // Act
            builder.WriteHeader(header);
            var buffer = builder.GetBuffer().Span;

            // Assert - Verify complete header structure
            Assert.Equal((byte)Magic.ClientRequest, buffer[0]); // Magic
            Assert.Equal((byte)OpCode.GetErrorMap, buffer[1]); // OpCode
            Assert.Equal(0x00, buffer[2]); // Extras length
            Assert.Equal((byte)DataType.Json, buffer[5]); // Data type
            Assert.Equal(0x00, buffer[6]); // VBucket high (fixed)
            Assert.Equal(0x00, buffer[7]); // VBucket low (fixed)

            var opaque = ByteConverter.ToUInt32(buffer.Slice(12), useNbo:false);
            Assert.Equal(0xABCDEF01u, opaque);

            var cas = ByteConverter.ToUInt64(buffer.Slice(16), useNbo:true);
            Assert.Equal(0x1122334455667788u, cas);
        }
    }
}
