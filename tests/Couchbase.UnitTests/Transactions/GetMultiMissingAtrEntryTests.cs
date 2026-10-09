#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Client.Transactions;
using Couchbase.Client.Transactions.Config;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Client.Transactions.Internal;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// NCBC-4324: get-multi reads X staged by another transaction T1, and T1's ATR entry is missing.
/// T1 may have committed and been removed, or expired before commit and been cleaned up. Per the
/// spec ("Implementation if T1's ATR entry is missing") get-multi must read T1's documents again to
/// tell these apart, rather than settle on the first read.
/// </summary>
public class GetMultiMissingAtrEntryTests
{
    private const string DocX = "doc-x";
    private const string DocY = "doc-y";

    private class Doc
    {
        public string Name { get; set; } = "";
    }

    private static Mock<ICouchbaseCollection> BuildCollection()
    {
        var bucket = new Mock<IBucket>();
        bucket.Setup(b => b.Name).Returns("b");
        var scope = new Mock<IScope>();
        scope.Setup(s => s.Name).Returns("s");
        scope.Setup(s => s.Bucket).Returns(bucket.Object);
        var collection = new Mock<ICouchbaseCollection>();
        collection.Setup(c => c.Name).Returns("c");
        collection.Setup(c => c.Scope).Returns(scope.Object);
        bucket.Setup(b => b.DefaultCollection()).Returns(collection.Object);

        // T1's ATR document exists but holds no entry for T1's attempt.
        var atrLookup = new Mock<ILookupInResult>();
        atrLookup.Setup(r => r.Exists(0)).Returns(false);
        collection.Setup(c => c.LookupInAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<LookupInSpec>>(), It.IsAny<LookupInOptions?>()))
            .ReturnsAsync(atrLookup.Object);
        return collection;
    }

    private static DocumentLookupResult Committed(ICouchbaseCollection collection, string id, string name) =>
        new(id,
            unstagedContent: new TranscodedContentWrapper(new Doc { Name = name }, new JsonTranscoder()),
            stagedContent: null,
            Mock.Of<ILookupInResult>(),
            documentMetadata: null,
            collection);

    // X as T1 left it: the pre-T1 body, plus T1's staged replace.
    private static DocumentLookupResult StagedByT1(ICouchbaseCollection collection) =>
        new(DocX,
            unstagedContent: new TranscodedContentWrapper(new Doc { Name = "pre-T1" }, new JsonTranscoder()),
            stagedContent: new TranscodedContentWrapper(new Doc { Name = "T1" }, new JsonTranscoder()),
            Mock.Of<ILookupInResult>(),
            documentMetadata: null,
            collection)
        {
            TransactionXattrs = new TransactionXattrs
            {
                Id = new CompositeId { Transactionid = "txn-t1", AttemptId = "attempt-t1", OperationId = "op-t1" },
                AtrRef = new AtrRef { Id = "_txn:atr-t1", BucketName = "b", ScopeName = "s", CollectionName = "c" },
                Operation = new StagedOperation { Type = "replace" },
            }
        };

    private static (AttemptContext ctx, Mock<IDocumentRepository> docs) BuildContext(
        Mock<ICouchbaseCollection> collection, DocumentLookupResult secondReadOfX)
    {
        var docs = new Mock<IDocumentRepository>();
        docs.SetupSequence(d => d.LookupDocumentAsync(
                It.IsAny<ICouchbaseCollection>(), DocX, It.IsAny<DateTimeOffset>(), It.IsAny<ITypeTranscoder?>(), It.IsAny<bool>()))
            .ReturnsAsync(StagedByT1(collection.Object))
            .ReturnsAsync(secondReadOfX);
        docs.Setup(d => d.LookupDocumentAsync(
                It.IsAny<ICouchbaseCollection>(), DocY, It.IsAny<DateTimeOffset>(), It.IsAny<ITypeTranscoder?>(), It.IsAny<bool>()))
            .ReturnsAsync(() => Committed(collection.Object, DocY, "y"));

        var overallContext = new TransactionContext("txn-ours", DateTimeOffset.UtcNow, new TransactionsConfig(), null);
        var atr = new AtrRepository("attempt-ours", overallContext, collection.Object, "_txn:atr-ours",
            atrDurability: null, NullLoggerFactory.Instance);

        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(ITypeTranscoder))).Returns(new JsonTranscoder());
        var cluster = new Mock<ICluster>();
        cluster.Setup(c => c.ClusterServices).Returns(services.Object);

        var ctx = new AttemptContext(
            overallContext: overallContext,
            attemptId: "attempt-ours",
            testHooks: null,
            redactor: new Redactor(RedactionLevel.None),
            loggerFactory: NullLoggerFactory.Instance,
            cluster: cluster.Object,
            documentRepository: docs.Object,
            atrRepository: atr);
        return (ctx, docs);
    }

    private static List<TransactionGetMultiSpec> Specs(ICouchbaseCollection collection) =>
    [
        new(collection, DocX, null),
        new(collection, DocY, null),
    ];

    private static void VerifyReadsOfX(Mock<IDocumentRepository> docs, int times) =>
        docs.Verify(d => d.LookupDocumentAsync(
            It.IsAny<ICouchbaseCollection>(), DocX, It.IsAny<DateTimeOffset>(), It.IsAny<ITypeTranscoder?>(),
            It.IsAny<bool>()), Times.Exactly(times));

    [Fact]
    public async Task GetMulti_T1AtrEntryMissing_T1Committed_ReturnsCommittedBody()
    {
        // T1 committed and its entry was removed between get-multi's first and second reads of X.
        var collection = BuildCollection();
        var (ctx, docs) = BuildContext(collection, Committed(collection.Object, DocX, "T1"));

        var result = await ctx.GetMulti(Specs(collection.Object));

        Assert.Equal("T1", result.ContentAs<Doc>(0)!.Name);
        Assert.Equal("y", result.ContentAs<Doc>(1)!.Name);
        VerifyReadsOfX(docs, 2);
    }

    [Fact]
    public async Task GetMulti_T1AtrEntryMissing_T1NeverCommitted_ReturnsPreT1Body()
    {
        // T1 expired before commit and its entry was cleaned up, so X is still staged on the second
        // read and T1 will never commit: the pre-T1 body is correct.
        var collection = BuildCollection();
        var (ctx, docs) = BuildContext(collection, StagedByT1(collection.Object));

        var result = await ctx.GetMulti(Specs(collection.Object));

        Assert.Equal("pre-T1", result.ContentAs<Doc>(0)!.Name);
        Assert.Equal("y", result.ContentAs<Doc>(1)!.Name);
        VerifyReadsOfX(docs, 2);
    }
}
