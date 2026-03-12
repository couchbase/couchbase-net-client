#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Buffers;
using System.Linq;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Couchbase.Protostellar.KV.V1;
using Couchbase.Stellar.Core;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Stellar.Core;

public class ProtoTypeConversionsTests
{
    [Fact]
    public void AsGetResult_GetResponse_WithCompressedContent_CallsDecompress()
    {
        var response = new GetResponse { ContentCompressed = ByteString.CopyFrom(new byte[] { 1, 2, 3 }), Cas = 456, Expiry = Timestamp.FromDateTime(DateTime.UtcNow) };
        AssertCompressedDecompresses(
            (t, c) => response.AsGetResult(t, c, null),
            originalBytes: new byte[] { 1, 2, 3 }
        );
    }

    [Fact]
    public void AsGetResult_GetAndLockResponse_WithCompressedContent_CallsDecompress()
    {
        var response = new GetAndLockResponse { ContentCompressed = ByteString.CopyFrom(new byte[] { 1, 2, 3 }), Cas = 456, Expiry = Timestamp.FromDateTime(DateTime.UtcNow) };
        AssertCompressedDecompresses(
            (t, c) => response.AsGetResult(t, c, null),
            originalBytes: new byte[] { 1, 2, 3 }
        );
    }

    [Fact]
    public void AsGetResult_GetAndTouchResponse_WithCompressedContent_CallsDecompress()
    {
        var response = new GetAndTouchResponse { ContentCompressed = ByteString.CopyFrom(new byte[] { 1, 2, 3 }), Cas = 456, Expiry = Timestamp.FromDateTime(DateTime.UtcNow) };
        AssertCompressedDecompresses(
            (t, c) => response.AsGetResult(t, c, null),
            originalBytes: new byte[] { 1, 2, 3 }
        );
    }

    [Fact]
    public void AsGetResult_GetResponse_WithUncompressedContent_DoesNotCallDecompress()
    {
        var response = new GetResponse { ContentUncompressed = ByteString.CopyFrom(new byte[] { 7, 8, 9 }), Cas = 456 };
        AssertUncompressedRemainsUncompressed(
            (t, c) => response.AsGetResult(t, c, null),
            uncompressedBytes: new byte[] { 7, 8, 9 }
        );
    }

    [Fact]
    public void AsGetResult_GetAndLockResponse_WithUncompressedContent_DoesNotCallDecompress()
    {
        var response = new GetAndLockResponse { ContentUncompressed = ByteString.CopyFrom(new byte[] { 7, 8, 9 }), Cas = 456 };
        AssertUncompressedRemainsUncompressed(
            (t, c) => response.AsGetResult(t, c, null),
            uncompressedBytes: new byte[] { 7, 8, 9 }
        );
    }

    [Fact]
    public void AsGetResult_GetAndTouchResponse_WithUncompressedContent_DoesNotCallDecompress()
    {
        var response = new GetAndTouchResponse { ContentUncompressed = ByteString.CopyFrom(new byte[] { 7, 8, 9 }), Cas = 456 };
        AssertUncompressedRemainsUncompressed(
            (t, c) => response.AsGetResult(t, c, null),
            uncompressedBytes: new byte[] { 7, 8, 9 }
        );
    }

    private void AssertCompressedDecompresses(Func<ITypeTranscoder, IOperationCompressor, IGetResult> invokeAsGetResult, byte[] originalBytes)
    {
        // Arrange
        var transcoder = new RawBinaryTranscoder();
        var mockCompressor = new Mock<IOperationCompressor>();
        var decompressedBytes = new byte[] { 4, 5, 6 };
        var memoryOwner = new TestMemoryOwner<byte>(decompressedBytes);

        mockCompressor.Setup(x => x.Decompress(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IRequestSpan>()))
            .Returns(memoryOwner);

        // Act
        var result = invokeAsGetResult(transcoder, mockCompressor.Object);

        // Assert
        mockCompressor.Verify(x => x.Decompress(It.Is<ReadOnlyMemory<byte>>(m => m.ToArray().SequenceEqual(originalBytes)), It.IsAny<IRequestSpan>()), Times.Once);
        Assert.Equal(456ul, result.Cas);
        
        // Assert the content was successfully decompressed and passed down to transcoder
        var resultContent = result.ContentAs<byte[]>();
        Assert.Equal(decompressedBytes, resultContent);
    }

    private void AssertUncompressedRemainsUncompressed(Func<ITypeTranscoder, IOperationCompressor, IGetResult> invokeAsGetResult, byte[] uncompressedBytes)
    {
        // Arrange
        var transcoder = new RawBinaryTranscoder();
        var mockCompressor = new Mock<IOperationCompressor>();

        // Act
        var result = invokeAsGetResult(transcoder, mockCompressor.Object);

        // Assert
        mockCompressor.Verify(x => x.Decompress(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IRequestSpan>()), Times.Never);
        Assert.Equal(456ul, result.Cas);

        // Assert the content matches uncompressed
        var resultContent = result.ContentAs<byte[]>();
        Assert.Equal(uncompressedBytes, resultContent);
    }

    [Fact]
    public void AsGetResult_GetResponse_NullCompressor_WithCompressedContent_FallsBackToUncompressed()
    {
        // When compressor is null (compression disabled), compressed content should fall through
        // to the uncompressed path — ContentUncompressed will be empty.
        var response = new GetResponse
        {
            ContentUncompressed = ByteString.CopyFrom(new byte[] { 10, 20, 30 }),
            Cas = 789
        };
        var transcoder = new RawBinaryTranscoder();

        var result = response.AsGetResult(transcoder, null, null);

        Assert.Equal(789ul, result.Cas);
        var content = result.ContentAs<byte[]>();
        Assert.Equal(new byte[] { 10, 20, 30 }, content);
    }

    [Fact]
    public void AsGetResult_GetAndLockResponse_NullCompressor_ReturnsUncompressedContent()
    {
        var response = new GetAndLockResponse
        {
            ContentUncompressed = ByteString.CopyFrom(new byte[] { 10, 20, 30 }),
            Cas = 789,
            Expiry = Timestamp.FromDateTime(DateTime.UtcNow)
        };
        var transcoder = new RawBinaryTranscoder();

        var result = response.AsGetResult(transcoder, null, null);

        Assert.Equal(789ul, result.Cas);
        var content = result.ContentAs<byte[]>();
        Assert.Equal(new byte[] { 10, 20, 30 }, content);
    }

    [Fact]
    public void AsGetResult_GetAndTouchResponse_NullCompressor_ReturnsUncompressedContent()
    {
        var response = new GetAndTouchResponse
        {
            ContentUncompressed = ByteString.CopyFrom(new byte[] { 10, 20, 30 }),
            Cas = 789,
            Expiry = Timestamp.FromDateTime(DateTime.UtcNow)
        };
        var transcoder = new RawBinaryTranscoder();

        var result = response.AsGetResult(transcoder, null, null);

        Assert.Equal(789ul, result.Cas);
        var content = result.ContentAs<byte[]>();
        Assert.Equal(new byte[] { 10, 20, 30 }, content);
    }

    [Fact]
    public void GetResult_Dispose_ReleasesDecompressedBuffer()
    {
        // Arrange - simulate a compressed response being decompressed
        var response = new GetResponse
        {
            ContentCompressed = ByteString.CopyFrom(new byte[] { 1, 2, 3 }),
            Cas = 100,
            Expiry = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        var mockMemoryOwner = new Mock<IMemoryOwner<byte>>();
        mockMemoryOwner.Setup(x => x.Memory).Returns(new Memory<byte>(new byte[] { 4, 5, 6 }));

        var mockCompressor = new Mock<IOperationCompressor>();
        mockCompressor.Setup(x => x.Decompress(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IRequestSpan>()))
            .Returns(mockMemoryOwner.Object);

        var transcoder = new RawBinaryTranscoder();

        // Act
        var result = response.AsGetResult(transcoder, mockCompressor.Object, null);
        result.Dispose();

        // Assert - verify the decompressed memory owner was disposed
        mockMemoryOwner.Verify(x => x.Dispose(), Times.Once);
    }

    private class TestMemoryOwner<T> : IMemoryOwner<T>
    {
        private readonly T[] _array;

        public TestMemoryOwner(T[] array)
        {
            _array = array;
        }

        public Memory<T> Memory => new Memory<T>(_array);

        public void Dispose()
        {
        }
    }
}
#endif
