#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Core;
using Couchbase.Client.Transactions;
using Couchbase.Client.Transactions.Config;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.Internal;
using Couchbase.Client.Transactions.Support;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// Regression tests for CBSE-22995 / NCBC-4036.
///
/// Bug: transaction Insert/Replace with default options ignored the cluster-configured
/// custom serializer and staged content with a freshly-created <c>new JsonTranscoder()</c>
/// (whose DefaultSerializer uses a camelCase contract resolver). Documents written inside a
/// transaction therefore had camelCase property names while non-transactional writes used the
/// configured (e.g. PascalCase) serializer — silent data corruption. This worked in 3.7.2 and
/// regressed in 3.9.x when the content wrapper was rewritten to eagerly encode in its constructor.
///
/// The fix routes staging through AttemptContext's <c>_defaultUserTranscoder</c>, which is built
/// from the cluster serializer via <see cref="NonStreamingSerializerWrapper.FromCluster"/>.
/// </summary>
public class CustomSerializerStagingRegressionTests
{
    // Mirrors the bug report's document: PascalCase properties that must survive staging.
    private class PascalCaseDoc
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
    }

    // A Newtonsoft serializer configured (as in the bug report) to preserve PascalCase.
    private static ITypeSerializer PascalCaseSerializer()
    {
        var settings = new JsonSerializerSettings { ContractResolver = new DefaultContractResolver() };
        return new DefaultSerializer(settings, settings);
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
        // InitAtrIfNeeded reads this before short-circuiting on the injected _atr.
        bucket.Setup(b => b.DefaultCollection()).Returns(collection.Object);
        return collection;
    }

    private static Mock<ICluster> BuildClusterWith(ITypeSerializer clusterSerializer)
    {
        // NonStreamingSerializerWrapper.FromCluster resolves the cluster transcoder's serializer.
        var services = new Mock<IServiceProvider>();
        services.Setup(s => s.GetService(typeof(ITypeTranscoder)))
            .Returns(new JsonTranscoder(clusterSerializer));
        var cluster = new Mock<ICluster>();
        cluster.Setup(c => c.ClusterServices).Returns(services.Object);
        return cluster;
    }

    /// <summary>
    /// THE regression test: drives AttemptContext.InsertAsync with default options (no per-op
    /// transcoder) and verifies the content handed to staging is encoded with the cluster's
    /// PascalCase serializer, not a camelCase default. Reverting the call site to the old
    /// camelCase default makes this fail.
    /// </summary>
    [Fact]
    public async Task InsertAsync_DefaultOptions_StagesWithClusterSerializer_NotCamelCase()
    {
        var collection = BuildCollection();
        var cluster = BuildClusterWith(PascalCaseSerializer());

        // Capture the wrapper that staging receives.
        IContentAsWrapper? captured = null;
        var docs = new Mock<IDocumentRepository>();
        docs.Setup(d => d.MutateStagedInsert(
                It.IsAny<ICouchbaseCollection>(), It.IsAny<string>(), It.IsAny<IContentAsWrapper>(),
                It.IsAny<string>(), It.IsAny<IAtrRepository>(), It.IsAny<ulong?>(), It.IsAny<DateTimeOffset?>()))
            .Callback<ICouchbaseCollection, string, IContentAsWrapper, string, IAtrRepository, ulong?, DateTimeOffset?>(
                (_, _, content, _, _, _, _) => captured = content)
            .ReturnsAsync((123UL, MutationToken.Empty));

        // Injecting an ATR repository short-circuits InitAtrIfNeeded so no real ATR machinery runs.
        var atr = new Mock<IAtrRepository>(collection.Object, "atr-id");

        var redactor = new Mock<IRedactor>();
        redactor.Setup(r => r.UserData(It.IsAny<object?>())).Returns((object? m) => m);

        var ctx = new AttemptContext(
            overallContext: new TransactionContext("txn-1", DateTimeOffset.UtcNow, new TransactionsConfig(), null),
            attemptId: "attempt-1",
            testHooks: null,
            redactor: redactor.Object,
            loggerFactory: NullLoggerFactory.Instance,
            cluster: cluster.Object,
            documentRepository: docs.Object,
            atrRepository: atr.Object);

        await ctx.InsertAsync(collection.Object, "doc-1", new PascalCaseDoc { FirstName = "John", LastName = "Doe" });

        Assert.NotNull(captured);
        var json = Encoding.UTF8.GetString(captured!.ContentAs<byte[]>()!);
        Assert.Contains("\"FirstName\"", json);
        Assert.Contains("\"LastName\"", json);
        Assert.DoesNotContain("\"firstName\"", json);
    }

    /// <summary>
    /// Negative control documenting the original 3.9.x failure mechanism: the parameterless
    /// <see cref="JsonTranscoder"/> (the wrapper's silent default) produces camelCase. This is
    /// exactly what staging used before the fix, which is why callers must pass the cluster transcoder.
    /// </summary>
    [Fact]
    public void DefaultJsonTranscoder_ProducesCamelCase_ReproducingTheBug()
    {
        var wrapper = new TranscodedContentWrapper(
            new PascalCaseDoc { FirstName = "John", LastName = "Doe" }, new JsonTranscoder());

        var json = Encoding.UTF8.GetString(wrapper.ContentAs<byte[]>()!);

        Assert.Contains("\"firstName\"", json);          // camelCase — the bug
        Assert.DoesNotContain("\"FirstName\"", json);
    }

    /// <summary>
    /// End-to-end staging pipeline: content encoded with the cluster's PascalCase serializer is
    /// written into the StagedData sub-doc spec verbatim (via the JsonElement pass-through), so the
    /// staged JSON keeps PascalCase property names.
    /// </summary>
    [Fact]
    public async Task MutateStagedInsert_PreservesClusterSerializerCasing_InStagedData()
    {
        var pascalSerializer = PascalCaseSerializer();
        var collection = BuildCollection();

        IEnumerable<MutateInSpec>? capturedSpecs = null;
        var mutateResult = new Mock<IMutateInResult>();
        mutateResult.Setup(r => r.Cas).Returns(1UL);
        mutateResult.Setup(r => r.MutationToken).Returns(MutationToken.Empty);
        collection.Setup(c => c.MutateInAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<MutateInSpec>>(), It.IsAny<MutateInOptions?>()))
            .Callback<string, IEnumerable<MutateInSpec>, MutateInOptions?>((_, specs, _) => capturedSpecs = specs)
            .ReturnsAsync(mutateResult.Object);

        var repo = new DocumentRepository(
            new TransactionContext("txn-1", DateTimeOffset.UtcNow, new TransactionsConfig(), null),
            keyValueTimeout: TimeSpan.FromSeconds(10),
            durability: DurabilityLevel.None,
            attemptId: "attempt-1",
            userDataSerializer: pascalSerializer);

        var atr = new Mock<IAtrRepository>(collection.Object, "atr-id");

        // Content wrapper built exactly as AttemptContext does for default options.
        var wrapper = new TranscodedContentWrapper(
            new PascalCaseDoc { FirstName = "John", LastName = "Doe" }, new JsonTranscoder(pascalSerializer));

        await repo.MutateStagedInsert(collection.Object, "doc-1", wrapper, "op-1", atr.Object);

        Assert.NotNull(capturedSpecs);
        var stagedSpec = capturedSpecs!.Single(s => s.Path == TransactionFields.StagedData);
        var stagedJson = (JsonElement)stagedSpec.Value!;
        var propNames = stagedJson.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Contains("FirstName", propNames);
        Assert.Contains("LastName", propNames);
        Assert.DoesNotContain("firstName", propNames);
    }
}
