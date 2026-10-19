#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Protostellar.KV.V1;
using Couchbase.Stellar;
using Couchbase.Stellar.Core.Retry;
using Couchbase.Stellar.KeyValue;
using Couchbase.UnitTests.Stellar.Utils;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Stellar.KeyValue;

public class StellarCollectionTests
{
    private async Task<(StellarCollection collection, Mock<IOperationCompressor> mockCompressor)> SetupStellarCollectionWithMockCompressor()
    {
        var mockCompressor = new Mock<IOperationCompressor>();
        var cluster = StellarMocks.CreateClusterFromMocks(mockCompressor.Object);

        // Setup a mock MemoryOwner for the mock compressor to return
        var decompressedBytes = new byte[] { 1, 2, 3 };
        var memoryOwner = new TestMemoryOwner<byte>(decompressedBytes);
        mockCompressor.Setup(x => x.Compress(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IRequestSpan>()))
            .Returns(memoryOwner);

        // Retrieve the default collection
        var bucket = (StellarBucket)await cluster.BucketAsync("test");
        var collection = (StellarCollection)bucket.DefaultCollection();

        return (collection, mockCompressor);
    }

    [Fact]
    public async Task InsertAsync_InvokesOperationCompressor()
    {
        // Arrange
        var (collection, mockCompressor) = await SetupStellarCollectionWithMockCompressor();
        var dummyContent = new { foo = "bar" };

        // Act - the retry orchestrator mock returns a successful response,
        // so this completes without hitting the network
        await collection.InsertAsync("my-key", dummyContent);

        // Assert
        mockCompressor.Verify(x => x.Compress(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IRequestSpan>()), Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_InvokesOperationCompressor()
    {
        // Arrange
        var (collection, mockCompressor) = await SetupStellarCollectionWithMockCompressor();
        var dummyContent = new { foo = "bar" };

        // Act - the retry orchestrator mock returns a successful response
        await collection.UpsertAsync("my-key", dummyContent);

        // Assert
        mockCompressor.Verify(x => x.Compress(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IRequestSpan>()), Times.Once);
    }

    [Fact]
    public async Task ReplaceAsync_InvokesOperationCompressor()
    {
        // Arrange
        var (collection, mockCompressor) = await SetupStellarCollectionWithMockCompressor();
        var dummyContent = new { foo = "bar" };

        // Act - the retry orchestrator mock returns a successful response
        await collection.ReplaceAsync("my-key", dummyContent);

        // Assert
        mockCompressor.Verify(x => x.Compress(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IRequestSpan>()), Times.Once);
    }

    [Fact]
    public async Task InsertAsync_WhenCompressionDisabled_DoesNotInvokeCompressor()
    {
        // Arrange - no compressor passed, so IsCompressionEnabled = false
        var (collection, mockCompressor) = await SetupStellarCollectionWithoutCompression();
        var dummyContent = new { foo = "bar" };

        // Act
        await collection.InsertAsync("my-key", dummyContent);

        // Assert
        mockCompressor.Verify(x => x.Compress(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IRequestSpan>()), Times.Never);
    }

    [Fact]
    public async Task UpsertAsync_WhenCompressionDisabled_DoesNotInvokeCompressor()
    {
        // Arrange
        var (collection, mockCompressor) = await SetupStellarCollectionWithoutCompression();
        var dummyContent = new { foo = "bar" };

        // Act
        await collection.UpsertAsync("my-key", dummyContent);

        // Assert
        mockCompressor.Verify(x => x.Compress(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IRequestSpan>()), Times.Never);
    }

    [Fact]
    public async Task ReplaceAsync_WhenCompressionDisabled_DoesNotInvokeCompressor()
    {
        // Arrange
        var (collection, mockCompressor) = await SetupStellarCollectionWithoutCompression();
        var dummyContent = new { foo = "bar" };

        // Act
        await collection.ReplaceAsync("my-key", dummyContent);

        // Assert
        mockCompressor.Verify(x => x.Compress(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IRequestSpan>()), Times.Never);
    }

    [Fact]
    public void IsCompressionEnabled_WithSnappyAndCompressionTrue_ReturnsTrue()
    {
        var cluster = StellarMocks.CreateClusterFromMocks(new Mock<IOperationCompressor>().Object);
        Assert.True(cluster.IsCompressionEnabled);
    }

    [Fact]
    public void IsCompressionEnabled_WithNoAlgorithm_ReturnsFalse()
    {
        // No compressor passed → CompressionAlgorithm.None → IsCompressionEnabled = false
        var cluster = StellarMocks.CreateClusterFromMocks();
        Assert.False(cluster.IsCompressionEnabled);
    }

    [Fact]
    public void IsCompressionEnabled_WithCompressionOptionFalse_ReturnsFalse()
    {
        var cluster = StellarMocks.CreateClusterFromMocksWithOptions(
            compressor: new Mock<IOperationCompressor>().Object,
            configureOptions: o => o.Compression = false);
        Assert.False(cluster.IsCompressionEnabled);
    }

    private async Task<(StellarCollection collection, Mock<IOperationCompressor> mockCompressor)> SetupStellarCollectionWithoutCompression()
    {
        var mockCompressor = new Mock<IOperationCompressor>();
        // Create cluster without passing compressor → IsCompressionEnabled = false
        var cluster = StellarMocks.CreateClusterFromMocks();

        var bucket = (StellarBucket)await cluster.BucketAsync("test");
        var collection = (StellarCollection)bucket.DefaultCollection();

        return (collection, mockCompressor);
    }

    private class TestMemoryOwner<T>(T[] array) : IMemoryOwner<T>
    {
        public Memory<T> Memory => new(array);

        public void Dispose()
        {
        }
    }

    #region MaxDocSize pre-flight checks

    private const int AtMaxDocSize = (int)MultiplexingConnection.MaxDocSize;
    private const int MaxDocSizeAllowed = AtMaxDocSize - 1;

    /// <summary>
    /// Creates a StellarCollection whose transcoder writes <paramref name="payloadSize"/>
    /// bytes on Encode, giving precise control over the encoded document size.
    /// </summary>
    private static async Task<StellarCollection> SetupCollectionWithSizedTranscoder(int payloadSize)
    {
        var mockTranscoder = new Mock<ITypeTranscoder>();
        mockTranscoder
            .Setup(t => t.Encode(It.IsAny<Stream>(), It.IsAny<object>(), It.IsAny<Flags>(), It.IsAny<OpCode>()))
            .Callback<Stream, object, Flags, OpCode>((stream, _, _, _) =>
            {
                var buf = new byte[payloadSize];
                stream.Write(buf, 0, buf.Length);
            });
        mockTranscoder
            .Setup(t => t.GetFormat(It.IsAny<object>()))
            .Returns(new Flags());

        var cluster = StellarMocks.CreateClusterFromMocksWithOptions(
            configureOptions: o => o.Transcoder = mockTranscoder.Object);
        var bucket = (StellarBucket)await cluster.BucketAsync("test");
        return (StellarCollection)bucket.DefaultCollection();
    }

    // --- Documents at MaxDocSize should throw ---

    [Fact]
    public async Task InsertAsync_WhenContentExceedsMaxDocSize_ThrowsValueToolargeException()
    {
        var collection = await SetupCollectionWithSizedTranscoder(AtMaxDocSize);

        await Assert.ThrowsAsync<ValueToolargeException>(
            () => collection.InsertAsync("key", new { data = "x" }));
    }

    [Fact]
    public async Task UpsertAsync_WhenContentExceedsMaxDocSize_ThrowsValueToolargeException()
    {
        var collection = await SetupCollectionWithSizedTranscoder(AtMaxDocSize);

        await Assert.ThrowsAsync<ValueToolargeException>(
            () => collection.UpsertAsync("key", new { data = "x" }));
    }

    [Fact]
    public async Task ReplaceAsync_WhenContentExceedsMaxDocSize_ThrowsValueToolargeException()
    {
        var collection = await SetupCollectionWithSizedTranscoder(AtMaxDocSize);

        await Assert.ThrowsAsync<ValueToolargeException>(
            () => collection.ReplaceAsync("key", new { data = "x" }));
    }

    [Fact]
    public async Task AppendAsync_WhenContentExceedsMaxDocSize_ThrowsValueToolargeException()
    {
        var cluster = StellarMocks.CreateClusterFromMocks();
        var bucket = (StellarBucket)await cluster.BucketAsync("test");
        var collection = (StellarCollection)bucket.DefaultCollection();

        var oversizedPayload = new byte[AtMaxDocSize];

        await Assert.ThrowsAsync<ValueToolargeException>(
            () => collection.AppendAsync("key", oversizedPayload));
    }

    [Fact]
    public async Task PrependAsync_WhenContentExceedsMaxDocSize_ThrowsValueToolargeException()
    {
        var cluster = StellarMocks.CreateClusterFromMocks();
        var bucket = (StellarBucket)await cluster.BucketAsync("test");
        var collection = (StellarCollection)bucket.DefaultCollection();

        var oversizedPayload = new byte[AtMaxDocSize];

        await Assert.ThrowsAsync<ValueToolargeException>(
            () => collection.PrependAsync("key", oversizedPayload));
    }

    // --- Documents just under MaxDocSize should pass the size check ---

    [Fact]
    public async Task InsertAsync_WhenContentJustUnderMaxDocSize_DoesNotThrowValueTooLarge()
    {
        var collection = await SetupCollectionWithSizedTranscoder(MaxDocSizeAllowed);

        // The call will fail downstream (no retry mock), but should NOT fail the size check.
        var ex = await Record.ExceptionAsync(() => collection.InsertAsync("key", new { data = "x" }));
        Assert.IsNotType<ValueToolargeException>(ex);
    }

    [Fact]
    public async Task UpsertAsync_WhenContentJustUnderMaxDocSize_DoesNotThrowValueTooLarge()
    {
        var collection = await SetupCollectionWithSizedTranscoder(MaxDocSizeAllowed);

        var ex = await Record.ExceptionAsync(() => collection.UpsertAsync("key", new { data = "x" }));
        Assert.IsNotType<ValueToolargeException>(ex);
    }

    [Fact]
    public async Task ReplaceAsync_WhenContentJustUnderMaxDocSize_DoesNotThrowValueTooLarge()
    {
        var collection = await SetupCollectionWithSizedTranscoder(MaxDocSizeAllowed);

        var ex = await Record.ExceptionAsync(() => collection.ReplaceAsync("key", new { data = "x" }));
        Assert.IsNotType<ValueToolargeException>(ex);
    }

    #endregion

    [Fact]
    public async Task GetAsync_WithProjectionsAndNullPayload_PreservesNull()
    {
        // Arrange
        var mockOrchestrator = new Mock<IRetryOrchestrator>();
        var getResponse = new Couchbase.Protostellar.KV.V1.GetResponse
        {
            Cas = 1,
            ContentUncompressed = Google.Protobuf.ByteString.CopyFromUtf8("null")
        };

        mockOrchestrator
            .Setup(x => x.RetryAsync(It.IsAny<Func<Task<Couchbase.Protostellar.KV.V1.GetResponse>>>(), It.IsAny<IRequest>()))
            .ReturnsAsync(getResponse);

        var cluster = StellarMocks.CreateClusterFromMocksWithOptions(
            requestOrchestratorMock: mockOrchestrator);

        var bucket = (StellarBucket)await cluster.BucketAsync("test");
        var collection = (StellarCollection)bucket.DefaultCollection();

        // Act
        var options = new GetOptions()
            .Projection("foo")
            .Transcoder(new JsonTranscoder(DefaultSerializer.Instance));

        var result = await collection.GetAsync("key", options);
        var content = result.ContentAs<dynamic>();

        // Assert
        // CNG's own null response for this edge case is passed straight through, matching
        // documented CNG behavior (see FIT's GetTest.getWithProjectionNoPathsExist), rather
        // than being substituted for an empty JSON object.
        Assert.Null(content);
    }

    #region GetAllReplicasStreamAsync

    // The gateway sends every read that succeeds and then ends the stream with the error of a read
    // that failed. For a missing document, like the classic stream, that read is left out rather
    // than faulting the stream.
    [Fact]
    public async Task ReadReplicaStreamAsync_Ends_The_Stream_When_A_Read_Finds_No_Document()
    {
        var collection = await CreateCollectionAsync();
        var stream = new FakeReplicaStream(
            [Replica(isReplica: false, cas: 1), Replica(isReplica: true, cas: 2)],
            new RpcException(new Status(StatusCode.NotFound, "document not found")));

        var results = await ReadAllAsync(collection, stream, CancellationToken.None);

        Assert.Equal([1ul, 2ul], results.Select(result => result.Cas));
        Assert.Equal([true, false], results.Select(result => result.IsActive));
    }

    [Fact]
    public async Task ReadReplicaStreamAsync_Is_Empty_When_The_Document_Is_Missing()
    {
        var collection = await CreateCollectionAsync();
        var stream = new FakeReplicaStream([], new RpcException(new Status(StatusCode.NotFound, "document not found")));

        Assert.Empty(await ReadAllAsync(collection, stream, CancellationToken.None));
    }

    // An error for the whole call, such as an unreachable gateway, is not a read to leave out.
    [Fact]
    public async Task ReadReplicaStreamAsync_Throws_The_Mapped_Error_For_Any_Other_Failure()
    {
        var collection = await CreateCollectionAsync();
        var stream = new FakeReplicaStream([], new RpcException(new Status(StatusCode.PermissionDenied, "denied")));

        await Assert.ThrowsAsync<AuthenticationFailureException>(() =>
            ReadAllAsync(collection, stream, CancellationToken.None));
    }

    [Fact]
    public async Task ReadReplicaStreamAsync_Throws_When_The_Caller_Cancels()
    {
        var collection = await CreateCollectionAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var stream = new FakeReplicaStream([], new RpcException(new Status(StatusCode.Cancelled, "cancelled")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReadAllAsync(collection, stream, cts.Token));
    }

    private static readonly ITypeTranscoder Transcoder = new JsonTranscoder(DefaultSerializer.Instance);

    private static async Task<List<IGetReplicaResult>> ReadAllAsync(StellarCollection collection,
        FakeReplicaStream stream, CancellationToken token)
    {
        var retryHandler = new StellarRetryHandler(TimeProvider.System, NullLogger<StellarRetryHandler>.Instance);
        var request = new StellarRequest { Idempotent = true, Token = token, Timeout = TimeSpan.FromSeconds(2.5) };

        var results = new List<IGetReplicaResult>();
        await foreach (var result in collection.ReadReplicaStreamAsync(stream, Transcoder, "key",
                           e => retryHandler.MapMidStreamException(e, request), token))
        {
            results.Add(result);
        }

        return results;
    }

    private static async Task<StellarCollection> CreateCollectionAsync()
    {
        var bucket = (StellarBucket)await StellarMocks.CreateClusterFromMocks().BucketAsync("test");
        return (StellarCollection)bucket.DefaultCollection();
    }

    private static GetAllReplicasResponse Replica(bool isReplica, ulong cas) => new()
    {
        IsReplica = isReplica,
        Cas = cas,
        Content = Google.Protobuf.ByteString.CopyFromUtf8("{}")
    };

    /// <summary>
    /// Returns the given responses, then throws the given error, as the gateway's stream does when
    /// a read failed.
    /// </summary>
    private sealed class FakeReplicaStream(GetAllReplicasResponse[] responses, Exception endError)
        : IAsyncStreamReader<GetAllReplicasResponse>
    {
        private int _next;

        public GetAllReplicasResponse Current { get; private set; } = null!;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (_next < responses.Length)
            {
                Current = responses[_next++];
                return Task.FromResult(true);
            }

            return Task.FromException<bool>(endError);
        }
    }

    #endregion
}
#endif
