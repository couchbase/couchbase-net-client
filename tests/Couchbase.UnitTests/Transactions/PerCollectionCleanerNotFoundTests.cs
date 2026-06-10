using System;
using System.Collections.Generic;
using Couchbase.Client.Transactions.Cleanup.LostTransactions;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Retry;
using Xunit;
using TimeoutException = Couchbase.Core.Exceptions.TimeoutException;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// Tests for <see cref="PerCollectionCleaner.IsCollectionNotFound"/>: true only when a cleanup op timed
/// out and the SOLE retry reason was collection/scope-not-found. Mixed or other reasons keep retrying.
/// </summary>
public class PerCollectionCleanerNotFoundTests
{
    private static TimeoutException TimeoutWith(params RetryReason[] reasons) =>
        new(new KeyValueErrorContext { RetryReasons = new List<RetryReason>(reasons) });

    [Fact]
    public void NonTimeout_ReturnsFalse() =>
        Assert.False(PerCollectionCleaner.IsCollectionNotFound(new InvalidOperationException()));

    [Fact]
    public void Timeout_NoRetryReasons_ReturnsFalse() =>
        Assert.False(PerCollectionCleaner.IsCollectionNotFound(new TimeoutException("timed out")));

    [Fact]
    public void Timeout_CollectionNotFoundOnly_ReturnsTrue() =>
        Assert.True(PerCollectionCleaner.IsCollectionNotFound(TimeoutWith(RetryReason.CollectionNotFound)));

    [Fact]
    public void Timeout_CollectionAndScopeNotFound_ReturnsTrue() =>
        Assert.True(PerCollectionCleaner.IsCollectionNotFound(
            TimeoutWith(RetryReason.CollectionNotFound, RetryReason.ScopeNotFound)));

    [Fact]
    public void Timeout_MixedReasons_ReturnsFalse() =>
        Assert.False(PerCollectionCleaner.IsCollectionNotFound(
            TimeoutWith(RetryReason.CollectionNotFound, RetryReason.KvErrorMapRetryIndicated)));
}
