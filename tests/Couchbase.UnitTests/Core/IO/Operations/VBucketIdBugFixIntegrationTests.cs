using System;
using System.Buffers;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    /// <summary>
    /// Integration tests that verify the complete VBucketId bug fix works end-to-end.
    /// These tests ensure that the fix works correctly in realistic usage scenarios and
    /// that no regressions are introduced for other operations.
    /// </summary>
    public class VBucketIdBugFixIntegrationTests
    {
        [Fact]
        public void GetErrorMap_CompleteWorkflow_ProducesCorrectPacket()
        {
            // Arrange - Simulate the complete workflow from operation creation to packet bytes
            using var builder = new OperationBuilder();

            // Create GetErrorMap exactly as it would be used in production
            using var operation = new GetErrorMap
            {
                VBucketId = 30066, // The exact value that caused the bug
                Opaque = 0x12345678
            };

            // Act - Execute the complete packet building process
            operation.WriteExtras(builder);
            operation.WriteKey(builder);
            operation.WriteBody(builder);

            var header = operation.CreateHeader(DataType.Json);
            builder.WriteHeader(header);

            var packet = builder.GetBuffer();

            // Assert - Verify the bug is fixed
            Assert.True(packet.Length >= 24, "Packet should contain at least header + body");

            var span = packet.Span;
            // The critical fix: VBucket bytes should be 0x00 0x00, not 0x75 0x72
            Assert.Equal(0x00, span[6]);
            Assert.Equal(0x00, span[7]);

            // Verify other fields are correct
            Assert.Equal((byte)Magic.AltRequest, span[0]);
            Assert.Equal((byte)OpCode.GetErrorMap, span[1]);
        }

        [Fact]
        public void MultipleGetErrorMapOperations_AllHaveZeroVBucket()
        {
            // Arrange - Test multiple operations to ensure consistency
            var problematicValues = new short[] { 30066, 12345, 999, 0, short.MaxValue };

            foreach (var vbucketValue in problematicValues)
            {
                using var builder = new OperationBuilder();
                using var operation = new GetErrorMap
                {
                    VBucketId = vbucketValue,
                    Opaque = (uint)vbucketValue
                };

                // Act
                operation.WriteExtras(builder);
                operation.WriteKey(builder);
                operation.WriteBody(builder);

                var header = operation.CreateHeader(DataType.Json);
                builder.WriteHeader(header);

                var packet = builder.GetBuffer().Span;

                // Assert - All should have VBucket = 0
                Assert.Equal(0x00, packet[6]);
                Assert.Equal(0x00, packet[7]);
            }
        }

        [Fact]
        public void NormalOperations_StillWorkCorrectly()
        {
            // Arrange - Verify that the fix doesn't break normal KV operations
            using var builder = new OperationBuilder();

            var header = new OperationRequestHeader
            {
                OpCode = OpCode.Get,
                VBucketId = 1024,
                Opaque = 0xDEADBEEF,
                DataType = DataType.Json
            };

            // Act
            builder.WriteHeader(header);
            var packet = builder.GetBuffer().Span;

            // Assert - Normal operations should still write VBucket correctly
            Assert.Equal(0x04, packet[6]); // High byte of 1024
            Assert.Equal(0x00, packet[7]); // Low byte of 1024

            Assert.Equal((byte)OpCode.Get, packet[1]);
            Assert.Equal(0x00, packet[2]); // Key length high
            Assert.Equal(0x00, packet[3]); // Key length low
        }

        [Fact]
        public void BufferReuse_DoesNotLeakGarbageData()
        {
            // Arrange - Test the scenario that originally caused the bug

            // Step 1: Create an operation that writes the problematic value
            using (var builder1 = new OperationBuilder())
            {
                var header1 = new OperationRequestHeader
                {
                    OpCode = OpCode.Get,
                    VBucketId = 30066, // This writes 0x7572
                    Opaque = 0x11111111
                };

                builder1.WriteHeader(header1);
                var buffer1 = builder1.GetBuffer();
                Assert.Equal(0x75, buffer1.Span[6]);
                Assert.Equal(0x72, buffer1.Span[7]);
            } // Buffer returned to pool here

            // Step 2: Create GetErrorMap operation - should not inherit garbage
            using var builder2 = new OperationBuilder();
            using var operation = new GetErrorMap { VBucketId = 999 };

            // Act
            operation.WriteExtras(builder2);
            operation.WriteKey(builder2);
            operation.WriteBody(builder2);

            var header2 = operation.CreateHeader(DataType.Json);
            builder2.WriteHeader(header2);

            var buffer2 = builder2.GetBuffer().Span;

            // Assert - Should be clean, not garbage from previous buffer
            Assert.Equal(0x00, buffer2[6]);
            Assert.Equal(0x00, buffer2[7]);
            Assert.NotEqual(0x75, buffer2[6]); // Not the garbage data
            Assert.NotEqual(0x72, buffer2[7]); // Not the garbage data
        }

        [Fact]
        public void OperationDisposal_CleansUpCorrectly()
        {
            // Arrange & Act - Verify operations can be created and disposed safely

            for (short? i = 0; i < 100; i++)
            {
                using var builder = new OperationBuilder();
                using var operation = new GetErrorMap
                {
                    VBucketId = (short?)(30066 + i), // Various values
                    Opaque = (uint)(0x10000000 + i)
                };

                operation.WriteExtras(builder);
                operation.WriteKey(builder);
                operation.WriteBody(builder);

                var header = operation.CreateHeader(DataType.Json);
                builder.WriteHeader(header);

                var packet = builder.GetBuffer().Span;

                // Assert - Every single operation should have VBucket = 0
                Assert.Equal(0x00, packet[6]);
                Assert.Equal(0x00, packet[7]);
                Assert.Equal((byte)OpCode.GetErrorMap, packet[1]);
            }
        }

        [Fact]
        public void CreateHeader_RequiresVBucketId_BehavesCorrectly()
        {
            // Arrange - Test both types of operations
            using var getErrorMapOp = new GetErrorMap { VBucketId = 12345 };
            using var getOp = new Get<string>();
            getOp.VBucketId = 12345;
            getOp.Key = "test-key";

            // Act
            var errorMapHeader = getErrorMapOp.CreateHeader(DataType.Json);
            var getHeader = getOp.CreateHeader(DataType.Json);

            // Assert - GetErrorMap should ignore VBucketId, Get should include it
            Assert.Null(errorMapHeader.VBucketId);
            Assert.False(getErrorMapOp.RequiresVBucketId);

            Assert.Equal((short?)12345, getHeader.VBucketId);
            Assert.True(getOp.RequiresVBucketId);
        }
    }
}
