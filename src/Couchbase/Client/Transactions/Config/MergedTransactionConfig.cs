#nullable enable
using Couchbase.KeyValue;
using Couchbase.Query;
using Microsoft.Extensions.Logging;
using System;

namespace Couchbase.Client.Transactions.Config
{
    internal record MergedTransactionConfig(
            bool CleanupClientAttempts,
            bool CleanupLostAttempts,
            TimeSpan CleanupWindow,
            DurabilityLevel DurabilityLevel,
            TimeSpan ExpirationTime,
            TimeSpan? KeyValueTimeout,
            ILoggerFactory? LoggerFactory,
            Keyspace? MetadataCollection,
            QueryScanConsistency? ScanConsistency)
    {
        public static MergedTransactionConfig Create(TransactionsConfig config, PerTransactionConfig? perConfig) =>
            new(
                CleanupClientAttempts: config.CleanupConfig.CleanupClientAttempts,
                CleanupLostAttempts: config.CleanupConfig.CleanupLostAttempts,
                CleanupWindow: config.CleanupConfig.CleanupWindow,
                DurabilityLevel: perConfig?.DurabilityLevel ?? config.DurabilityLevel,
                ExpirationTime: perConfig?.Timeout ?? config.ExpirationTime,
                KeyValueTimeout: perConfig?.KeyValueTimeout ?? config.KeyValueTimeout,
                LoggerFactory: config.LoggerFactory,
                MetadataCollection: perConfig?.MetadataCollection ?? config.MetadataCollection,
                ScanConsistency: perConfig?.ScanConsistency ?? config.ScanConsistency);
    }
}
