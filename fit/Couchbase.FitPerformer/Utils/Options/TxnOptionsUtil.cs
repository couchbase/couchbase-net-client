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

    public static Keyspace ConvertCollectionToKeyspace(Couchbase.Grpc.Protocol.Shared.Collection collection)
    {
        return new Keyspace(collection.BucketName, collection.ScopeName, collection.CollectionName);
    }
}
