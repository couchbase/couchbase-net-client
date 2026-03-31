using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Operations
{
    /// <summary>
    /// Unit tests for GetErrorMap operation, specifically verifying the VBucketId bug fix.
    /// The bug was that GetErrorMap operations were incorrectly including VBucketId in the packet,
    /// when they should always have VBucketId = 0.
    /// </summary>
    public class GetErrorMapTests
    {
        [Fact]
        public void RequiresVBucketId_ReturnsFalse()
        {
            // Arrange & Act
            using var operation = new GetErrorMap();

            // Assert - This is the core fix: GetErrorMap should not require VBucketId
            Assert.False(operation.RequiresVBucketId);
        }

        [Fact]
        public void CreateOperationRequestHeader_VBucketIdIsNull()
        {
            // Arrange
            using var operation = new GetErrorMap
            {
                VBucketId = 30066, // The problematic value from the bug report
                Opaque = 0x12345678,
                Cas = 0xABCDEF01
            };

            // Act
            var header = operation.CreateHeader(DataType.Json);

            // Assert - VBucketId should be null in the header, not the assigned value
            Assert.Null(header.VBucketId);
            Assert.Equal(0x12345678u, header.Opaque);
            Assert.Equal(0xABCDEF01u, header.Cas);
        }

        [Fact]
        public void CreateOperationRequestHeader_IgnoresAssignedVBucketId()
        {
            // Arrange - Test various VBucketId values to ensure they're all ignored
            var testValues = new short[] { 0, 1, 1024, 30066, short.MaxValue };

            foreach (var vbucketId in testValues)
            {
                using var operation = new GetErrorMap();
                operation.VBucketId = vbucketId;

                // Act
                var header = operation.CreateHeader(DataType.Json);

                // Assert - Should always be null regardless of assigned value
                Assert.Null(header.VBucketId);
            }
        }

        [Fact]
        public void GetErrorMap_DefaultProperties_AreCorrect()
        {
            // Arrange & Act
            using var operation = new GetErrorMap
            {
                VBucketId = 0
            };

            // Assert - Verify default operation properties
            Assert.Equal(OpCode.GetErrorMap, operation.OpCode);
            Assert.False(operation.RequiresVBucketId);
            Assert.Equal((short)0, operation.VBucketId);
        }

        [Fact]
        public void GetErrorMap_WithCustomOpaque_MaintainsCorrectBehavior()
        {
            // Arrange
            const uint expectedOpaque = 0xDEADBEEF;
            using var operation = new GetErrorMap
            {
                Opaque = expectedOpaque,
                VBucketId = 999 // Should be ignored
            };

            // Act
            var header = operation.CreateHeader(DataType.Json);

            // Assert
            Assert.Equal(expectedOpaque, header.Opaque);
            Assert.Null(header.VBucketId); // Still null despite VBucketId assignment
            Assert.False(operation.RequiresVBucketId);
        }

        [Fact]
        public void GetErrorMap_HeaderCreation_IsIdempotent()
        {
            // Arrange
            using var operation = new GetErrorMap
            {
                VBucketId = 12345,
                Opaque = 0x11111111
            };

            // Act - Create header multiple times
            var header1 = operation.CreateHeader(DataType.Json);
            var header2 = operation.CreateHeader(DataType.Json);
            var header3 = operation.CreateHeader(DataType.Json);

            // Assert - All headers should be identical
            Assert.Null(header1.VBucketId);
            Assert.Null(header2.VBucketId);
            Assert.Null(header3.VBucketId);

            Assert.Equal(header1.Opaque, header2.Opaque);
            Assert.Equal(header2.Opaque, header3.Opaque);
            Assert.Equal(0x11111111u, header1.Opaque);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(30066)] // The specific problematic value
        [InlineData(32767)] // Max short value
        public void GetErrorMap_VariousVBucketIdValues_AlwaysIgnored(short vbucketId)
        {
            // Arrange & Act
            using var operation = new GetErrorMap { VBucketId = vbucketId };
            var header = operation.CreateHeader(DataType.Json);

            // Assert - VBucketId should always be null regardless of input
            Assert.Null(header.VBucketId);
            Assert.False(operation.RequiresVBucketId);
        }
    }
}
