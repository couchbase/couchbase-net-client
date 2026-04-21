#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// Verifies that DocumentRepository.LookupDocumentAsync assigns the correct transcoder
/// to staged and unstaged content wrappers for each combination of staged data type.
/// </summary>
public class LookupDocumentTranscoderTests
{
    private static readonly ReadOnlyMemory<byte> JsonBytes =
        Encoding.UTF8.GetBytes("""{"key":"value"}""");
    private static readonly ReadOnlyMemory<byte> BinaryBytes = new byte[] { 0x01, 0x02, 0x03 };

    private static Mock<ILookupInResultInternal> BuildLookupResult(
        bool hasTransaction, bool hasJsonStaged, bool hasBinStaged)
    {
        var specs = new List<LookupInSpec>
        {
            new() { Bytes = JsonBytes },   // 0 txn xattrs
            new() { Bytes = JsonBytes },   // 1 $document meta
            new() { Bytes = JsonBytes },   // 2 staged JSON data
            new() { Bytes = BinaryBytes }, // 3 staged binary data
            new() { Bytes = JsonBytes },   // 4 full document body
        };

        var mock = new Mock<ILookupInResultInternal>();
        mock.Setup(r => r.Specs).Returns(specs);
        mock.Setup(r => r.Flags).Returns(new Flags { DataFormat = DataFormat.Json });
        mock.Setup(r => r.Exists(0)).Returns(hasTransaction);
        mock.Setup(r => r.Exists(2)).Returns(hasJsonStaged);
        mock.Setup(r => r.Exists(3)).Returns(hasBinStaged);
        mock.Setup(r => r.Exists(4)).Returns(true);
        mock.Setup(r => r.IsDeleted).Returns(false);
        mock.Setup(r => r.Cas).Returns(0UL);
        return mock;
    }

    private static ICouchbaseCollection BuildCollection(Mock<ILookupInResultInternal> lookupResult)
    {
        var mockBucket = new Mock<IBucket>();
        mockBucket.Setup(b => b.Name).Returns("b");
        var mockScope = new Mock<IScope>();
        mockScope.Setup(s => s.Name).Returns("s");
        mockScope.Setup(s => s.Bucket).Returns(mockBucket.Object);
        var mockCollection = new Mock<ICouchbaseCollection>();
        mockCollection.Setup(c => c.Name).Returns("c");
        mockCollection.Setup(c => c.Scope).Returns(mockScope.Object);
        mockCollection
            .Setup(c => c.LookupInAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<LookupInSpec>>(),
                It.IsAny<LookupInOptions?>()))
            .ReturnsAsync(lookupResult.Object);
        return mockCollection.Object;
    }

    [Fact]
    public async Task JsonStaged_StagedContentUsesUserTranscoder()
    {
        var mock = BuildLookupResult(hasTransaction: true, hasJsonStaged: true, hasBinStaged: false);
        var userTranscoder = new JsonTranscoder(new DefaultSerializer());
        var defaultTranscoder = new JsonTranscoder(SystemTextJsonSerializer.Create());

        var result = await DocumentRepository.LookupDocumentAsync(
            BuildCollection(mock), "doc-id", keyValueTimeout: null,
            userTranscoder: userTranscoder, defaultJsonTranscoder: defaultTranscoder);

        Assert.NotNull(result.StagedContent);
        Assert.Same(userTranscoder, result.StagedContent!.Transcoder);
    }

    [Fact]
    public async Task JsonStaged_UnstagedContentUsesDefaultJsonTranscoder()
    {
        var mock = BuildLookupResult(hasTransaction: true, hasJsonStaged: true, hasBinStaged: false);
        var defaultTranscoder = new JsonTranscoder(SystemTextJsonSerializer.Create());

        var result = await DocumentRepository.LookupDocumentAsync(
            BuildCollection(mock), "doc-id", keyValueTimeout: null,
            defaultJsonTranscoder: defaultTranscoder);

        Assert.Same(defaultTranscoder, result.UnstagedContent?.Transcoder);
    }

    [Fact]
    public async Task BinaryStaged_StagedContentUsesRawBinaryTranscoder()
    {
        var mock = BuildLookupResult(hasTransaction: true, hasJsonStaged: false, hasBinStaged: true);

        var result = await DocumentRepository.LookupDocumentAsync(
            BuildCollection(mock), "doc-id", keyValueTimeout: null,
            defaultJsonTranscoder: new JsonTranscoder(SystemTextJsonSerializer.Create()));

        Assert.NotNull(result.StagedContent);
        Assert.IsType<RawBinaryTranscoder>(result.StagedContent!.Transcoder);
    }

    [Fact]
    public async Task BinaryStaged_UnstagedContentAlsoUsesRawBinaryTranscoder()
    {
        // Pre-transaction content must use RawBinaryTranscoder when staged content is binary —
        // the document was binary before the transaction too.
        var mock = BuildLookupResult(hasTransaction: true, hasJsonStaged: false, hasBinStaged: true);

        var result = await DocumentRepository.LookupDocumentAsync(
            BuildCollection(mock), "doc-id", keyValueTimeout: null,
            defaultJsonTranscoder: new JsonTranscoder(SystemTextJsonSerializer.Create()));

        Assert.IsType<RawBinaryTranscoder>(result.UnstagedContent?.Transcoder);
    }

    [Fact]
    public async Task RemoveOperation_StagedContentIsNull()
    {
        // Transaction exists but no staged data field (remove produces no staged document)
        var mock = BuildLookupResult(hasTransaction: true, hasJsonStaged: false, hasBinStaged: false);

        var result = await DocumentRepository.LookupDocumentAsync(
            BuildCollection(mock), "doc-id", keyValueTimeout: null,
            defaultJsonTranscoder: new JsonTranscoder(SystemTextJsonSerializer.Create()));

        Assert.Null(result.StagedContent);
        Assert.NotNull(result.UnstagedContent);
    }

    [Fact]
    public async Task NoTransaction_StagedContentIsNull_UnstagedUsesDefaultJsonTranscoder()
    {
        var mock = BuildLookupResult(hasTransaction: false, hasJsonStaged: false, hasBinStaged: false);
        var defaultTranscoder = new JsonTranscoder(SystemTextJsonSerializer.Create());

        var result = await DocumentRepository.LookupDocumentAsync(
            BuildCollection(mock), "doc-id", keyValueTimeout: null,
            defaultJsonTranscoder: defaultTranscoder);

        Assert.Null(result.StagedContent);
        Assert.NotNull(result.UnstagedContent);
        Assert.Same(defaultTranscoder, result.UnstagedContent!.Transcoder);
    }
}
