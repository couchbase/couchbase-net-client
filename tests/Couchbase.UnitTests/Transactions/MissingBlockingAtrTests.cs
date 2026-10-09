#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Client.Transactions;
using Couchbase.Client.Transactions.Config;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Client.Transactions.Error.Attempts;
using Couchbase.Client.Transactions.Forwards;
using Couchbase.Client.Transactions.Internal;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// NCBC-4323: a document carries staged changes from another transaction whose ATR document no
/// longer exists (e.g. the ATR collection was dropped and recreated). Per BF-CBD-3705 a get must
/// treat a missing ATR document like a missing ATR entry and return the pre-transaction body, and
/// CheckWriteWriteConflict must treat it as "cleanup has occurred" and proceed. Mirrors FIT's
/// CustomMetadataCollectionTest.failTransactionAtReplaceRemoveStaging_DeleteRecreateATRCollectionAndExecuteGet_ExpectSuccess.
/// </summary>
public class MissingBlockingAtrTests
{
    private const string DocId = "doc-1";
    private const ulong PreTransactionCas = 100;

    private class Doc
    {
        public string Name { get; set; } = "";
    }

    private static Mock<ICouchbaseCollection> BuildCollection(Exception? atrLookupError = null)
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

        // By default the blocking transaction's ATR document is gone: the server answers the ATR
        // lookup with KEY_ENOENT, which the SDK surfaces as DocumentNotFoundException.
        collection.Setup(c => c.LookupInAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<LookupInSpec>>(), It.IsAny<LookupInOptions?>()))
            .ThrowsAsync(atrLookupError ?? new DocumentNotFoundException());
        return collection;
    }

    // The document as left by the failed transaction: pre-transaction body, plus that
    // transaction's staged replace, pointing at an ATR in the same collection.
    private static DocumentLookupResult BlockedDocument(ICouchbaseCollection collection)
    {
        var lookupInResult = new Mock<ILookupInResult>();
        lookupInResult.Setup(r => r.IsDeleted).Returns(false);
        lookupInResult.Setup(r => r.Cas).Returns(PreTransactionCas);

        return new DocumentLookupResult(
            DocId,
            unstagedContent: new TranscodedContentWrapper(new Doc { Name = "pre" }, new JsonTranscoder()),
            stagedContent: new TranscodedContentWrapper(new Doc { Name = "post" }, new JsonTranscoder()),
            lookupInResult.Object,
            documentMetadata: null,
            collection)
        {
            TransactionXattrs = new TransactionXattrs
            {
                Id = new CompositeId { Transactionid = "txn-other", AttemptId = "attempt-other", OperationId = "op-other" },
                AtrRef = new AtrRef { Id = "_txn:atr-other", BucketName = "b", ScopeName = "s", CollectionName = "c" },
                Operation = new StagedOperation { Type = "replace" },
            }
        };
    }

    private static AttemptContext BuildContext(Mock<ICouchbaseCollection> collection)
    {
        var docs = new Mock<IDocumentRepository>();
        docs.Setup(d => d.LookupDocumentAsync(
                It.IsAny<ICouchbaseCollection>(), DocId, It.IsAny<bool>(), It.IsAny<ITypeTranscoder?>(), It.IsAny<bool>()))
            .ReturnsAsync(() => BlockedDocument(collection.Object));

        var overallContext = new TransactionContext("txn-1", DateTimeOffset.UtcNow, new TransactionsConfig(), null);

        // A real AtrRepository, so the ATR lookup runs the production FindEntryForTransaction
        // against the collection above.
        var atr = new AtrRepository("attempt-1", overallContext, collection.Object, "_txn:atr-ours",
            atrDurability: null, NullLoggerFactory.Instance);

        // AttemptContext resolves the cluster transcoder for user content.
        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(ITypeTranscoder))).Returns(new JsonTranscoder());
        var cluster = new Mock<ICluster>();
        cluster.Setup(c => c.ClusterServices).Returns(services.Object);

        return new AttemptContext(
            overallContext: overallContext,
            attemptId: "attempt-1",
            testHooks: null,
            redactor: new Redactor(RedactionLevel.None),
            loggerFactory: NullLoggerFactory.Instance,
            cluster: cluster.Object,
            documentRepository: docs.Object,
            atrRepository: atr);
    }

    [Fact]
    public async Task GetAsync_BlockingAtrDocumentMissing_ReturnsPreTransactionBody()
    {
        var collection = BuildCollection();
        var ctx = BuildContext(collection);

        var result = await ctx.GetAsync(collection.Object, DocId);

        Assert.Equal(PreTransactionCas, result.Cas);
        Assert.Equal("pre", result.ContentAs<Doc>()!.Name);
    }

    [Fact]
    public async Task GetOptionalAsync_BlockingAtrDocumentMissing_DoesNotReportDocumentAbsent()
    {
        var collection = BuildCollection();
        var ctx = BuildContext(collection);

        var result = await ctx.GetOptionalAsync(collection.Object, DocId);

        Assert.NotNull(result);
        Assert.Equal("pre", result!.ContentAs<Doc>()!.Name);
    }

    [Fact]
    public async Task CheckWriteWriteConflict_BlockingAtrDocumentMissing_Proceeds()
    {
        var collection = BuildCollection();
        var ctx = BuildContext(collection);
        var blocked = BlockedDocument(collection.Object).GetPreTransactionResult();

        // Spec: "If the ATR entry does not exist, or the ATR is no longer present, then cleanup
        // has occurred. It is ok to proceed."
        await ctx.CheckWriteWriteConflict(blocked, ForwardCompatibility.WriteWriteConflictReplacing, parentSpan: null);

        // Proceeding must come from the missing ATR, not from an early return before the lookup.
        collection.Verify(c => c.LookupInAsync(
            "_txn:atr-other", It.IsAny<IEnumerable<LookupInSpec>>(), It.IsAny<LookupInOptions?>()), Times.Once);
    }

    // The Get tests above pass if either the AtrRepository catch or the triage arm handles the
    // missing ATR document, so each layer is pinned on its own below.

    [Fact]
    public async Task FindEntryForTransaction_AtrDocumentMissing_ReturnsNullAndWarns()
    {
        var collection = BuildCollection();
        var logger = new Mock<ILogger>();

        var entry = await AtrRepository.FindEntryForTransaction(collection.Object, "_txn:atr-other",
            "attempt-other", logger: logger.Object);

        Assert.Null(entry);
        logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task FindEntryForTransaction_AtrLookupFailsOtherwise_Throws()
    {
        // A dropped (not recreated) ATR collection surfaces as a timeout after the SDK retries
        // UnknownCollection. Only a missing document is BF-CBD-3705; any other failure must
        // still fail, or a transient KV problem would be read as "the ATR is gone".
        var collection = BuildCollection(new UnambiguousTimeoutException());

        await Assert.ThrowsAsync<UnambiguousTimeoutException>(() =>
            AtrRepository.FindEntryForTransaction(collection.Object, "_txn:atr-other", "attempt-other"));
    }

    [Fact]
    public void TriageAtrLookupInMavErrors_DocumentNotFound_RaisesAtrNotFoundUnwrapped()
    {
        // Unwrapped, so GetWithMav's BF-CBD-3705 handler catches it; a TransactionOperationFailed
        // wrapper would fail the transaction instead.
        var triage = new ErrorTriage(BuildContext(BuildCollection()), NullLoggerFactory.Instance);

        Assert.Throws<ActiveTransactionRecordNotFoundException>(() =>
            triage.TriageAtrLookupInMavErrors(new DocumentNotFoundException()));
    }
}
