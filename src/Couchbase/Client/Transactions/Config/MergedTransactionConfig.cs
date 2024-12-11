#nullable enable
using System;
using Couchbase.KeyValue;
using Couchbase.Query;
using Microsoft.Extensions.Logging;

namespace Couchbase.Client.Transactions.Config
{
    internal record MergedTransactionConfig(
            TransactionCleanupConfig CleanupConfig,
            DurabilityLevel DurabilityLevel,
            TimeSpan Timeout,
            ILoggerFactory? LoggerFactory,
            KeySpace? MetadataCollection,
            QueryScanConsistency? ScanConsistency)
    {
        public static MergedTransactionConfig Create(TransactionConfig config, PerTransactionConfig? perConfig) =>
            new(
                CleanupConfig: config.CleanupConfig ?? new(),
                DurabilityLevel: perConfig?.DurabilityLevel ?? config.DurabilityLevel,
                Timeout: perConfig?.Timeout ?? config.Timeout ?? TransactionConfig.DefaultTimeout,
                LoggerFactory: config.LoggerFactory,
                MetadataCollection: config.MetadataCollection,
                ScanConsistency: perConfig?.ScanConsistency ?? config.ScanConsistency);
    }
}







