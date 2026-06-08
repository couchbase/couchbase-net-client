using Couchbase.Client.Transactions;
using Couchbase.Client.Transactions.Config;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// Tests for <see cref="Cluster.ShouldEagerlyInitializeTransactions"/>, which gates whether the
/// Transactions subsystem (and its cleanup machinery) is spun up at connect time. See NCBC-4218:
/// a default cluster must NOT eagerly start cleanup, since that permanently parks a thread-pool
/// thread and caused latency spikes for applications that do not use transactions.
/// </summary>
public class ClusterEagerTransactionsInitTests
{
    private static Keyspace SampleCollection => new("default", "_default", "_default");

    [Fact]
    public void DefaultConfig_DoesNotEagerlyInitialize()
    {
        // Default config has CleanupLostAttempts == true but no configured collections.
        var config = TransactionsConfigBuilder.Create().Build();

        Assert.True(config.CleanupConfig.CleanupLostAttempts, "precondition: lost cleanup defaults on");
        Assert.Empty(config.CleanupConfig.CollectionsList);
        Assert.Null(config.MetadataCollection);

        Assert.False(Cluster.ShouldEagerlyInitializeTransactions(config));
    }

    [Fact]
    public void ConfiguredCleanupCollection_EagerlyInitializes()
    {
        var config = TransactionsConfigBuilder.Create()
            .CleanupConfig(TransactionCleanupConfigBuilder.Create()
                .CleanupLostAttempts(true)
                .AddCollection(SampleCollection)
                .Build())
            .Build();

        Assert.True(Cluster.ShouldEagerlyInitializeTransactions(config));
    }

    [Fact]
    public void ConfiguredMetadataCollection_EagerlyInitializes()
    {
        var config = TransactionsConfigBuilder.Create()
            .MetadataCollection(SampleCollection)
            .Build();

        Assert.True(Cluster.ShouldEagerlyInitializeTransactions(config));
    }

    [Fact]
    public void LostCleanupDisabled_WithConfiguredCollection_DoesNotEagerlyInitialize()
    {
        var config = TransactionsConfigBuilder.Create()
            .CleanupConfig(TransactionCleanupConfigBuilder.Create()
                .CleanupLostAttempts(false)
                .AddCollection(SampleCollection)
                .Build())
            .Build();

        Assert.False(Cluster.ShouldEagerlyInitializeTransactions(config));
    }

    [Fact]
    public void LostCleanupDisabled_NoCollections_DoesNotEagerlyInitialize()
    {
        var config = TransactionsConfigBuilder.Create()
            .CleanupConfig(TransactionCleanupConfigBuilder.Create()
                .CleanupLostAttempts(false)
                .Build())
            .Build();

        Assert.False(Cluster.ShouldEagerlyInitializeTransactions(config));
    }
}
