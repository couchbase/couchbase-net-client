using System;
using Couchbase.Client.Transactions.Config;
using Moq;
using Xunit;
using CouchbaseTransactions = Couchbase.Client.Transactions.Transactions;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// Tests for creating a <see cref="CouchbaseTransactions"/> against a caller-supplied
/// <see cref="ICluster"/>. NCBC-4302 moved the redactor lookup to the internal concrete type, which an
/// outside implementation cannot register, so the lookup falls back instead of throwing.
/// </summary>
public class TransactionsForeignClusterTests
{
    // Both cleanups off: either one makes the constructor start background work.
    private static TransactionsConfig WithoutCleanup => TransactionsConfigBuilder.Create()
        .CleanupConfig(TransactionCleanupConfigBuilder.Create()
            .CleanupClientAttempts(false)
            .CleanupLostAttempts(false)
            .Build())
        .Build();

    [Fact]
    public void Create_WithClusterSupplyingNoServices_DoesNotThrow()
    {
        var cluster = new Mock<ICluster>();
        cluster.Setup(c => c.ClusterServices).Returns(new Mock<IServiceProvider>().Object);

        using var transactions = CouchbaseTransactions.Create(cluster.Object, WithoutCleanup);

        Assert.NotNull(transactions);
    }
}
