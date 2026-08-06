#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// NCBC-4261: on commit we must use the user flags recorded in txn.aux.uf at staging time,
/// not the live document body's flags. These cover the parse/fallback helper and that the
/// staged content wrapper surfaces the staged flags end-to-end through LookupDocumentAsync.
/// </summary>
public class StagedUserFlagsTests
{
    private static readonly ReadOnlyMemory<byte> JsonBytes =
        Encoding.UTF8.GetBytes("""{"key":"value"}""");
    private static readonly ReadOnlyMemory<byte> BinaryBytes = new byte[] { 0x01, 0x02, 0x03 };

    private static TransactionXattrs XattrsWithAux(string? auxJson) => new()
    {
        AuxiliaryData = auxJson is null
            ? null
            : JsonDocument.Parse(auxJson).RootElement.Clone()
    };

    // Builds an aux JSON object carrying the user flags for the given format, computed via ToUInt32
    // so the test stays correct regardless of the (network byte order) encoding.
    private static string AuxWithUf(DataFormat dataFormat, TypeCode typeCode = TypeCode.Object) =>
        $$"""{"uf":{{new Flags { DataFormat = dataFormat, TypeCode = typeCode }.ToUInt32()}}}""";

    #region ParseStagedUserFlags

    [Fact]
    public void ParseStagedUserFlags_JsonUf_ReturnsStagedFlags()
    {
        var flags = DocumentRepository.ParseStagedUserFlags(XattrsWithAux(AuxWithUf(DataFormat.Json)));

        Assert.Equal(DataFormat.Json, flags.DataFormat);
        Assert.Equal(TypeCode.Object, flags.TypeCode);
    }

    [Fact]
    public void ParseStagedUserFlags_BinaryUf_ReturnsBinaryFlags()
    {
        var flags = DocumentRepository.ParseStagedUserFlags(XattrsWithAux(AuxWithUf(DataFormat.Binary)));

        Assert.Equal(DataFormat.Binary, flags.DataFormat);
    }

    [Fact]
    public void ParseStagedUserFlags_NoAux_FallsBackToJsonCommonFlags()
    {
        var flags = DocumentRepository.ParseStagedUserFlags(XattrsWithAux(null));

        Assert.Equal(DataFormat.Json, flags.DataFormat);
        Assert.Equal(TypeCode.Object, flags.TypeCode);
    }

    [Fact]
    public void ParseStagedUserFlags_AuxWithoutUf_FallsBackToJsonCommonFlags()
    {
        var flags = DocumentRepository.ParseStagedUserFlags(XattrsWithAux("""{"docexpiry":123}"""));

        Assert.Equal(DataFormat.Json, flags.DataFormat);
    }

    [Fact]
    public void ParseStagedUserFlags_NullXattrs_FallsBackToJsonCommonFlags()
    {
        var flags = DocumentRepository.ParseStagedUserFlags(null);

        Assert.Equal(DataFormat.Json, flags.DataFormat);
    }

    #endregion

    #region End-to-end via LookupDocumentAsync

    private static Mock<ILookupInResultInternal> BuildLookupResult(Flags bodyFlags, TransactionXattrs txnXattrs)
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
        mock.Setup(r => r.Flags).Returns(bodyFlags);
        mock.Setup(r => r.ContentAs<TransactionXattrs>(0)).Returns(txnXattrs);
        mock.Setup(r => r.Exists(0)).Returns(true);
        mock.Setup(r => r.Exists(2)).Returns(true);   // JSON staged
        mock.Setup(r => r.Exists(3)).Returns(false);
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
    public async Task StagedContent_UsesUf_NotBodyFlags()
    {
        // Body flags deliberately differ from the staged uf to prove the source.
        var bodyFlags = new Flags { DataFormat = DataFormat.String };
        var mock = BuildLookupResult(bodyFlags, XattrsWithAux(AuxWithUf(DataFormat.Json)));

        var result = await DocumentRepository.LookupDocumentAsync(
            BuildCollection(mock), "doc-id", keyValueTimeout: null,
            defaultJsonTranscoder: new JsonTranscoder(SystemTextJsonSerializer.Create()));

        Assert.NotNull(result.StagedContent);
        Assert.Equal(DataFormat.Json, result.StagedContent!.Flags.DataFormat);
        Assert.Equal(TypeCode.Object, result.StagedContent.Flags.TypeCode);
    }

    [Fact]
    public async Task UnstagedContent_StillUsesBodyFlags()
    {
        var bodyFlags = new Flags { DataFormat = DataFormat.Json, TypeCode = TypeCode.String };
        var mock = BuildLookupResult(bodyFlags, XattrsWithAux(AuxWithUf(DataFormat.Json)));

        var result = await DocumentRepository.LookupDocumentAsync(
            BuildCollection(mock), "doc-id", keyValueTimeout: null,
            defaultJsonTranscoder: new JsonTranscoder(SystemTextJsonSerializer.Create()));

        Assert.NotNull(result.UnstagedContent);
        // Unstaged (pre-transaction) content keeps the live body flags, not the staged uf.
        Assert.Equal(TypeCode.String, result.UnstagedContent!.Flags.TypeCode);
    }

    [Fact]
    public async Task StagedContent_NoUf_FallsBackToJson()
    {
        var bodyFlags = new Flags { DataFormat = DataFormat.String };
        var mock = BuildLookupResult(bodyFlags, XattrsWithAux(null));

        var result = await DocumentRepository.LookupDocumentAsync(
            BuildCollection(mock), "doc-id", keyValueTimeout: null,
            defaultJsonTranscoder: new JsonTranscoder(SystemTextJsonSerializer.Create()));

        Assert.NotNull(result.StagedContent);
        Assert.Equal(DataFormat.Json, result.StagedContent!.Flags.DataFormat);
    }

    #endregion
}
