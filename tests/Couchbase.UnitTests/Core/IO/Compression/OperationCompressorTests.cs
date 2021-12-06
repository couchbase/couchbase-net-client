using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Compression;
using Couchbase.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Compression
{
    public class OperationCompressorTests
    {
        #region Compress

        [Fact]
        public void Compress_Disabled_ReturnsNull()
        {
            // Arrange

            var options = new ClusterOptions
            {
                Compression = false
            };

            var compressionAlgorithm = CreateMockCompressionAlgorithm(100);

            var compressor = new OperationCompressor(compressionAlgorithm, options,
                NullLogger<OperationCompressor>.Instance);

            // Act

            using var result = compressor.Compress(new byte[1024], NoopRequestSpan.Instance);

            // Assert

            Assert.Null(result);
        }

        [Fact]
        public void Compress_LessThanMinSize_ReturnsNull()
        {
            // Arrange

            var options = new ClusterOptions
            {
                CompressionMinSize = 32
            };

            var compressionAlgorithm = CreateMockCompressionAlgorithm(1);

            var compressor = new OperationCompressor(compressionAlgorithm, options,
                NullLogger<OperationCompressor>.Instance);

            // Act

            using var result = compressor.Compress(new byte[31], NoopRequestSpan.Instance);

            // Assert

            Assert.Null(result);
        }

        [Fact]
        public void Compress_EqualToMinSize_ReturnsCompressed()
        {
            // Arrange

            var options = new ClusterOptions
            {
                CompressionMinSize = 32
            };

            var compressionAlgorithm = CreateMockCompressionAlgorithm(1);

            var compressor = new OperationCompressor(compressionAlgorithm, options,
                NullLogger<OperationCompressor>.Instance);

            // Act

            using var result = compressor.Compress(new byte[32], NoopRequestSpan.Instance);

            // Assert

            Assert.NotNull(result);
        }

        [Fact]
        public void Compress_GreaterThanMinRatio_ReturnsNull()
        {
            // Arrange

            var options = new ClusterOptions
            {
                CompressionMinRatio = 0.80f
            };

            var compressionAlgorithm = CreateMockCompressionAlgorithm(81);

            var compressor = new OperationCompressor(compressionAlgorithm, options,
                NullLogger<OperationCompressor>.Instance);

            // Act

            using var result = compressor.Compress(new byte[100], NoopRequestSpan.Instance);

            // Assert

            Assert.Null(result);
        }

        [Fact]
        public void Compress_EqualToMinRatio_ReturnsNull()
        {
            // Arrange

            var options = new ClusterOptions
            {
                CompressionMinRatio = 0.80f
            };

            var compressionAlgorithm = CreateMockCompressionAlgorithm(80);

            var compressor = new OperationCompressor(compressionAlgorithm, options,
                NullLogger<OperationCompressor>.Instance);

            // Act

            using var result = compressor.Compress(new byte[100], NoopRequestSpan.Instance);

            // Assert

            Assert.NotNull(result);
        }

        #endregion

        #region Helpers

        public ICompressionAlgorithm CreateMockCompressionAlgorithm(int compressedSize)
        {
            var compressionAlgorithm = new Mock<ICompressionAlgorithm>();
            compressionAlgorithm
                .Setup(m => m.Algorithm)
                .Returns(CompressionAlgorithm.Snappy);
            compressionAlgorithm
                .Setup(m => m.Compress(It.IsAny<ReadOnlyMemory<byte>>()))
                .Returns((ReadOnlyMemory<byte> _) => MemoryPool<byte>.Shared.RentAndSlice(compressedSize));

            return compressionAlgorithm.Object;
        }

        #endregion
    }
}
