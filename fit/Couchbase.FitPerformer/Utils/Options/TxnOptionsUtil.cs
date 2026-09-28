using System;
using System.Linq;
using Couchbase.Query;
using Couchbase.Client.Transactions.Config;
using Couchbase.Client.Transactions;
using Couchbase.Client.Transactions;
namespace Couchbase.FitPerformer.Utils.Options;

public static class TxnOptionsUtil
{
    public static TransactionQueryOptions ConvertTransactionQueryOptions(
        Couchbase.Grpc.Protocol.Transactions.TransactionQueryOptions? protoOptions)
    {
        var options = new TransactionQueryOptions();
        if (protoOptions != null)
        {
            if (protoOptions.HasAdhoc) options.AdHoc(protoOptions.Adhoc);
            if (protoOptions.HasReadonly) options.Readonly(protoOptions.Readonly);
            if (protoOptions.HasFlexIndex) options.FlexIndex(protoOptions.FlexIndex);
            if (protoOptions.HasPipelineBatch) options.PipelineBatch(protoOptions.PipelineBatch);
            if (protoOptions.HasPipelineCap) options.PipelineCap(protoOptions.PipelineCap);
            if (protoOptions.HasScanCap) options.ScanCap(protoOptions.ScanCap);
            if (protoOptions.HasScanConsistency)
                options.ScanConsistency(
                    OptionsUtil.ConvertScanConsistency(protoOptions.ScanConsistency));
            if (protoOptions.HasScanWaitMillis)
                options.ScanWait(TimeSpan.FromMilliseconds(protoOptions.ScanWaitMillis));
            protoOptions.ParametersNamed?.ToList()
                .ForEach(kvp => options.Parameter(kvp.Key, kvp.Value));
            protoOptions.ParametersPositional?.ToList().ForEach(x => options.Parameter(x));
            protoOptions.Raw?.ToList().ForEach(kvp => options.Raw(kvp.Key, kvp.Value));
            if (protoOptions.HasProfile)
            {
                if (Enum.TryParse<QueryProfile>(protoOptions.Profile, ignoreCase: true,
                        out var profile))
                {
                    options.Profile(profile);
                }
            }
        }
        return options;
    }

    // Non-SDK-integration single query. The driver reuses the SDK QueryOptions message, but the .NET API only
    // accepts a TransactionQueryOptions here, so each field is mapped by hand. timeout_millis is applied at the
    // SingleQueryTransactionConfig level by the caller, as the proto requires. Fields TransactionQueryOptions cannot
    // carry fail loudly instead of being dropped.
    public static TransactionQueryOptions ConvertSingleQueryOptions(
        Couchbase.Grpc.Protocol.Sdk.Query.QueryOptions protoOptions)
    {
        var options = new TransactionQueryOptions();
        if (protoOptions.HasAdhoc) options.AdHoc(protoOptions.Adhoc);
        if (protoOptions.HasReadonly) options.Readonly(protoOptions.Readonly);
        if (protoOptions.HasFlexIndex) options.FlexIndex(protoOptions.FlexIndex);
        if (protoOptions.HasPipelineBatch) options.PipelineBatch(protoOptions.PipelineBatch);
        if (protoOptions.HasPipelineCap) options.PipelineCap(protoOptions.PipelineCap);
        if (protoOptions.HasScanCap) options.ScanCap(protoOptions.ScanCap);
        if (protoOptions.HasScanConsistency)
            options.ScanConsistency(OptionsUtil.ConvertScanConsistency(protoOptions.ScanConsistency));
        if (protoOptions.HasScanWaitMillis)
            options.ScanWait(TimeSpan.FromMilliseconds(protoOptions.ScanWaitMillis));
        if (protoOptions.HasClientContextId) options.ClientContextId(protoOptions.ClientContextId);
        if (protoOptions.HasProfile) options.Profile(OptionsUtil.ConvertQueryProfile(protoOptions.Profile));
        protoOptions.ParametersNamed?.ToList().ForEach(kvp => options.Parameter(kvp.Key, kvp.Value));
        protoOptions.ParametersPositional?.ToList().ForEach(x => options.Parameter(x));
        protoOptions.Raw?.ToList().ForEach(kvp => options.Raw(kvp.Key, kvp.Value));

        if (protoOptions.HasMaxParallelism) throw new NotSupportedException("max_parallelism is not available on TransactionQueryOptions");
        if (protoOptions.HasMetrics) throw new NotSupportedException("metrics is not available on TransactionQueryOptions");
        if (protoOptions.HasUseReplica) throw new NotSupportedException("use_replica is not available on TransactionQueryOptions");
        if (protoOptions.HasPreserveExpiry) throw new NotSupportedException("preserve_expiry is not available on TransactionQueryOptions");
        if (protoOptions.ConsistentWith != null) throw new NotSupportedException("consistent_with is not available on TransactionQueryOptions");

        return options;
    }

    public static Keyspace ConvertCollectionToKeyspace(Couchbase.Grpc.Protocol.Shared.Collection collection)
    {
        return new Keyspace(collection.BucketName, collection.ScopeName, collection.CollectionName);
    }
}
