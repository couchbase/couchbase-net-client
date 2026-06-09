using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Couchbase.FitPerformer.Utils;
using Couchbase.Grpc.Protocol.Sdk.Bucket.CollectionManager;
using Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager;
using Couchbase.Grpc.Protocol.Sdk.Collection.MutateIn;
using Couchbase.Grpc.Protocol.Sdk.Kv;
using Couchbase.Grpc.Protocol.Sdk.Kv.LookupIn;
using Couchbase.Grpc.Protocol.Sdk.Query.IndexManager;
using Couchbase.Grpc.Protocol.Sdk.Search.IndexManager;
using Couchbase.Grpc.Protocol.Shared;
using Couchbase.KeyValue;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BucketSettings = Couchbase.Management.Buckets.BucketSettings;
using Result = Couchbase.Grpc.Protocol.Sdk.Result;
using SearchIndex = Couchbase.Management.Search.SearchIndex;

#nullable enable

namespace Couchbase.FitPerformer.Utils.Results;

public static class ResultsUtil
{
    public static void SetSuccess(Couchbase.Grpc.Protocol.Run.Result result)
    {
        result.Sdk = new Result();
        result.Sdk.Success = true;
    }

    public static void PopulateResult(Couchbase.Grpc.Protocol.Run.Result result, IExistsResult value)
    {
        var builder = new Couchbase.Grpc.Protocol.Sdk.Kv.ExistsResult();
        builder.Cas = (long)value.Cas;
        builder.Exists = value.Exists;

        result.Sdk = new Result();
        result.Sdk.ExistsResult = builder;
    }

    public static void PopulateResult(Couchbase.Grpc.Protocol.Run.Result result, IGetReplicaResult value, ContentAs.AsOneofCase contentAs)
    {
        var builder = new Couchbase.Grpc.Protocol.Sdk.Kv.GetReplicaResult();
        builder.Cas = (long)value.Cas;
        builder.Content = CommandUtils.DeserializeContentType(contentAs, value);
        builder.IsReplica = !value.IsActive;
        if (value.ExpiryTime != null) builder.ExpiryTime = new DateTimeOffset(value.ExpiryTime.Value).ToUnixTimeSeconds();

        result.Sdk = new Grpc.Protocol.Sdk.Result();
        result.Sdk.GetReplicaResult = builder;
    }

    public static void PopulateResult(Couchbase.Grpc.Protocol.Run.Result result, IMutateInResult value, MutateIn request)
    {
        var builder = new Couchbase.Grpc.Protocol.Sdk.Collection.MutateIn.MutateInResult();
        builder.Cas = (long)value.Cas;
        var mt = value.MutationToken;
        builder.MutationToken = new MutationToken();
        builder.MutationToken.BucketName = mt.BucketRef;
        builder.MutationToken.PartitionId = mt.VBucketId;
        builder.MutationToken.PartitionUuid = mt.VBucketUuid;
        builder.MutationToken.SequenceNumber = mt.SequenceNumber;
        var index = 0;
        foreach (var mutateInSpec in request.Spec)
        {
            if (mutateInSpec.ContentAs == null)
            {
                builder.Results.Add(new MutateInSpecResult());
                index++;
                continue;
            }
            var content = new ContentOrError();
            content.Content = CommandUtils.DeserializeContentType(mutateInSpec.ContentAs.AsCase, value, index++);
            var specResult = new MutateInSpecResult();
            specResult.ContentAsResult = content;
            builder.Results.Add(specResult);
        }
        result.Sdk = new Result();
        result.Sdk.MutateInResult = builder;
    }

    public static void PopulateResult(Couchbase.Grpc.Protocol.Run.Result result, IMutationResult value)
    {
        var builder = new Couchbase.Grpc.Protocol.Sdk.Kv.MutationResult();
        builder.Cas = (long)value.Cas;
        var mt = value.MutationToken;
        builder.MutationToken = new MutationToken();
        builder.MutationToken.BucketName = mt.BucketRef;
        builder.MutationToken.PartitionId = mt.VBucketId;
        builder.MutationToken.PartitionUuid = mt.VBucketUuid;
        builder.MutationToken.SequenceNumber = mt.SequenceNumber;
        result.Sdk = new Result();
        result.Sdk.MutationResult = builder;
    }

    public static void PopulateResult(Couchbase.Grpc.Protocol.Run.Result result, ICounterResult value)
    {
        var builder = new Couchbase.Grpc.Protocol.Sdk.Kv.CounterResult();
        builder.Cas = (long)value.Cas;
        var mt = value.MutationToken;
        builder.MutationToken = new MutationToken();
        builder.MutationToken.BucketName = mt.BucketRef;
        builder.MutationToken.PartitionId = mt.VBucketId;
        builder.MutationToken.PartitionUuid = mt.VBucketUuid;
        builder.MutationToken.SequenceNumber = mt.SequenceNumber;
        builder.Content = (long)value.Content;
        result.Sdk = new Result();
        result.Sdk.CounterResult = builder;
    }

    public static void PopulateResult(Couchbase.Grpc.Protocol.Run.Result result, IGetResult value, ContentAs.AsOneofCase contentAs)
    {
        var builder = new Couchbase.Grpc.Protocol.Sdk.Kv.GetResult();
        builder.Cas = (long)value.Cas;
        builder.Content = new ContentTypes();
        builder.Content = CommandUtils.DeserializeContentType(contentAs, value);
        if (value.ExpiryTime != null)
        {
            builder.ExpiryTime = new DateTimeOffset(value.ExpiryTime.Value).ToUnixTimeSeconds();

        }
        result.Sdk = new Grpc.Protocol.Sdk.Result();
        result.Sdk.GetResult = builder;
    }

    public static dynamic ContentOrMacro(ContentOrMacro contentOrMacro)
    {
        return (contentOrMacro.ContentOrMacroCase switch
        {
            Grpc.Protocol.Sdk.Collection.MutateIn.ContentOrMacro.ContentOrMacroOneofCase.Content => Content(contentOrMacro.Content),
            Grpc.Protocol.Sdk.Collection.MutateIn.ContentOrMacro.ContentOrMacroOneofCase.Macro => Macro(contentOrMacro.Macro),
            _ => throw new NotSupportedException()
        })!;
    }

    public static dynamic? Content(Content content)
    {
        if (content.ContentCase == Couchbase.Grpc.Protocol.Shared.Content.ContentOneofCase.PassthroughString)
        {
            return content.PassthroughString;
        }

        if (content.ContentCase == Couchbase.Grpc.Protocol.Shared.Content.ContentOneofCase.ConvertToJson)
        {
            return JsonConvert.DeserializeObject<JObject>(content.ConvertToJson.ToStringUtf8());
        }

        if (content.ContentCase == Couchbase.Grpc.Protocol.Shared.Content.ContentOneofCase.ByteArray)
        {
            return content.ByteArray.ToByteArray();
        }

        if (content.ContentCase == Grpc.Protocol.Shared.Content.ContentOneofCase.Null)
        {
            return null!;
        }

        throw new NotSupportedException();
    }

    public static dynamic Macro(MutateInMacro macro)
    {
        return macro switch
        {
            MutateInMacro.Cas => MutationMacro.Cas,
            MutateInMacro.SeqNo => MutationMacro.SeqNo,
            MutateInMacro.ValueCrc32C => MutationMacro.ValueCRC32c,
            _ => throw new NotSupportedException()
        };
    }

    #region RangeScan
    public static Couchbase.Grpc.Protocol.Run.Result ProcessScanResult(Couchbase.KeyValue.RangeScan.IScanResult scanResult, string streamId, ContentAs? contentAs = null)
    {
        var result = new Couchbase.Grpc.Protocol.Run.Result();
        var builder = new Couchbase.Grpc.Protocol.Sdk.Kv.RangeScan.ScanResult();

        if (scanResult.Cas != 0) builder.Cas = (long)scanResult.Cas;
        if (scanResult.Id != null) builder.Id = scanResult.Id;
        if (!scanResult.IdOnly && contentAs != null) builder.Content = CommandUtils.DeserializeContentType(contentAs.AsCase, scanResult);
        if (scanResult.ExpiryTime.HasValue) builder.ExpiryTime = scanResult.ExpiryTime.Value.Second;

        builder.StreamId = streamId;
        builder.IdOnly = scanResult.IdOnly;

        result.Sdk = new Couchbase.Grpc.Protocol.Sdk.Result();
        result.Sdk.RangeScanResult = builder;

        return result;
    }
    #endregion

    #region LookupIn
    public static void ProcessLookupInResult(ILookupInResult lookupResult, Grpc.Protocol.Run.Result result, RepeatedField<Couchbase.Grpc.Protocol.Sdk.Kv.LookupIn.LookupInSpec> specs)
    {
        var builder = new  Couchbase.Grpc.Protocol.Sdk.Kv.LookupIn.LookupInResult();
        for (int i = 0; i < specs.Count; i++)
        {
            var specResult = new  Couchbase.Grpc.Protocol.Sdk.Kv.LookupIn.LookupInSpecResult
            {
                ContentAsResult = new ContentOrError(),
                ExistsResult = new BooleanOrError()
            };

            var contentAs = specs[i].ContentAs.AsCase;
            Serilog.Log.Debug("Adding a Spec({I}) Result with Exists = {E}", i, lookupResult.Exists(i));

            try
            {
                specResult.ExistsResult.Value = lookupResult.Exists(i);
            }
            catch (System.Exception e)
            {
                specResult.ExistsResult.Exception = ErrorsUtil.ConvertException(e);
            }

            try
            {
                var contentOrError = CommandUtils.DeserializeContentType(contentAs, lookupResult, i);
                specResult.ExistsResult.Value = lookupResult.Exists(i);

                if (contentOrError != null)
                {
                    specResult.ContentAsResult.Content = contentOrError;
                    Serilog.Log.Debug("Adding Spec({I}) Result with Content = {C}", i, specResult.ContentAsResult.Content);
                }
                else
                {
                    Serilog.Log.Debug("Spec({I}) Result has no content or error", i);
                }
            }
            catch (System.Exception e)
            {
                specResult.ContentAsResult.Exception = ErrorsUtil.ConvertException(e);
            }

            builder.Results.Add(specResult);
        }

        Serilog.Log.Debug("Builder has {C} elements", builder.Results.Count);

        result.Sdk = new Result();
        result.Sdk.LookupInResult = builder;

        Serilog.Log.Debug("Result has {C}", result.Sdk.LookupInResult.Results[0].ContentAsResult.ResultCase);
    }
    #endregion

    #region LookupInReplicas
    public static Grpc.Protocol.Run.Result ProcessLookupInAllReplicasResult(ILookupInReplicaResult lookupResult, RepeatedField<Couchbase.Grpc.Protocol.Sdk.Kv.LookupIn.LookupInSpec> specs, string streamId)
    {
        var result = new Grpc.Protocol.Run.Result();
        var builder = new LookupInAllReplicasResult
        {
            LookupInReplicaResult = new LookupInReplicaResult(),
            StreamId = streamId
        };

        builder.LookupInReplicaResult.Cas = (long)lookupResult.Cas;
        builder.LookupInReplicaResult.IsReplica = lookupResult.IsReplica ?? false;

        for (int i = 0; i < specs.Count; i++)
        {
            var specResult = new LookupInSpecResult
            {
                ContentAsResult = new ContentOrError(),
                ExistsResult = new BooleanOrError()
            };

            var contentAs = specs[i].ContentAs.AsCase;

            try
            {
                specResult.ExistsResult.Value = lookupResult.Exists(i);
            }
            catch (System.Exception e)
            {
                specResult.ExistsResult.Exception = ErrorsUtil.ConvertException(e);
            }

            try
            {
                var contentOrError = CommandUtils.DeserializeContentType(contentAs, lookupResult, i);
                specResult.ExistsResult.Value = lookupResult.Exists(i);

                if (contentOrError != null)
                {
                    specResult.ContentAsResult.Content = contentOrError;
                    Serilog.Log.Debug("Adding Spec({I}) Result with Content = {C}", i, specResult.ContentAsResult.Content);
                }
                else
                {
                    Serilog.Log.Debug("Spec({I}) Result has no content or error", i);
                }
            }
            catch (System.Exception e)
            {
                specResult.ContentAsResult.Exception = ErrorsUtil.ConvertException(e);
            }

            builder.LookupInReplicaResult.Results.Add(specResult);
        }

        result.Sdk = new Result();
        result.Sdk.LookupInAllReplicasResult = builder;
        return result;
    }

    public static void ProcessLookupInAnyReplicaResult(ILookupInReplicaResult lookupResult, Grpc.Protocol.Run.Result result, RepeatedField<Couchbase.Grpc.Protocol.Sdk.Kv.LookupIn.LookupInSpec> specs)
    {
        var builder = new LookupInReplicaResult();
        builder.IsReplica = lookupResult.IsReplica ?? false;

        for (int i = 0; i < specs.Count; i++)
        {
            var specResult = new LookupInSpecResult
            {
                ContentAsResult = new ContentOrError(),
                ExistsResult = new BooleanOrError()
            };
            var contentAs = specs[i].ContentAs.AsCase;

            Serilog.Log.Debug("Adding a Spec({I}) Result with Exists = {E}", i, lookupResult.Exists(i));

            try
            {
                specResult.ExistsResult.Value = lookupResult.Exists(i);
            }
            catch (System.Exception e)
            {
                specResult.ExistsResult.Exception = ErrorsUtil.ConvertException(e);
            }

            try
            {
                var contentOrError = CommandUtils.DeserializeContentType(contentAs, lookupResult, i);
                specResult.ExistsResult.Value = lookupResult.Exists(i);

                if (contentOrError != null)
                {
                    specResult.ContentAsResult.Content = contentOrError;
                    Serilog.Log.Debug("Adding Spec({I}) Result with Content = {C}", i, specResult.ContentAsResult.Content);
                }
                else
                {
                    Serilog.Log.Debug("Spec({I}) Result has no content or error", i);
                }
            }
            catch (System.Exception e)
            {
                specResult.ContentAsResult.Exception = ErrorsUtil.ConvertException(e);
            }

            builder.Results.Add(specResult);
        }

        result.Sdk = new Result();
        result.Sdk.LookupInAnyReplicaResult = builder;
    }
    #endregion
    public static void ConvertQueryIndexes(IEnumerable<Couchbase.Management.Query.QueryIndex> queryIndexes, Grpc.Protocol.Run.Result result)
    {
        var builder = new Couchbase.Grpc.Protocol.Sdk.Query.IndexManager.QueryIndexes();
        var stream = queryIndexes.GetEnumerator();

        while (stream.MoveNext())
        {
            var cbIndex = stream.Current;
            var protoIndex = new Couchbase.Grpc.Protocol.Sdk.Query.IndexManager.QueryIndex();

            if (cbIndex.Name != null) protoIndex.Name = cbIndex.Name;
            if (cbIndex.State != null) protoIndex.State = cbIndex.State;
            if (cbIndex.Keyspace != null) protoIndex.Keyspace = cbIndex.Keyspace;
            if (cbIndex.Condition != null) protoIndex.Condition = cbIndex.Condition;
            if (cbIndex.Partition != null) protoIndex.Partition = cbIndex.Partition;
            if (cbIndex.BucketName != null) protoIndex.BucketName = cbIndex.BucketName;
            if (cbIndex.ScopeName != null) protoIndex.ScopeName = cbIndex.ScopeName;
            //If 3.4.5 doesn't work, try 3.4.12
            if (cbIndex.CollectionName != null) protoIndex.CollectionName = cbIndex.CollectionName;
            if (cbIndex.IndexKey != null) protoIndex.IndexKey.AddRange(cbIndex.IndexKey);
            if (TryConvertQueryIndexType(cbIndex.Type, out var protoType)) protoIndex.Type = protoType;
            protoIndex.IsPrimary = cbIndex.IsPrimary;

            builder.Indexes.Add(protoIndex);

            Serilog.Log.Debug("Got index {Index} on {B}.{S}.{C}, Keyspace: {K}", protoIndex.Name, protoIndex.BucketName, protoIndex.ScopeName, protoIndex.CollectionName, protoIndex.Keyspace);
        }

        result.Sdk = new Result();
        result.Sdk.QueryIndexes = builder;
        stream.Dispose();
    }
    public static bool TryConvertQueryIndexType(Couchbase.Management.Views.IndexType couchbaseType, out Couchbase.Grpc.Protocol.Sdk.Query.IndexManager.QueryIndexType protoType)
    {
        switch (couchbaseType)
        {
            case Couchbase.Management.Views.IndexType.Gsi:
                protoType = QueryIndexType.Gsi;
                return true;
            case Couchbase.Management.Views.IndexType.View:
                protoType = QueryIndexType.View;
                return true;
            default:
                protoType = default;
                return false;
        }
    }
    public static Couchbase.KeyValue.LookupInSpec ConvertLookupInSpec(Couchbase.Grpc.Protocol.Sdk.Kv.LookupIn.LookupInSpec protoSpec)
    {
        switch (protoSpec.OperationCase)
        {
            case Grpc.Protocol.Sdk.Kv.LookupIn.LookupInSpec.OperationOneofCase.Get:
            {
                if (protoSpec.Get.HasXattr) return Couchbase.KeyValue.LookupInSpec.Get(protoSpec.Get.Path, protoSpec.Get.Xattr);
                return Couchbase.KeyValue.LookupInSpec.Get(protoSpec.Get.Path);
            }
            case Grpc.Protocol.Sdk.Kv.LookupIn.LookupInSpec.OperationOneofCase.Count:
            {
                if (protoSpec.Count.HasXattr) return Couchbase.KeyValue.LookupInSpec.Count(protoSpec.Count.Path, protoSpec.Count.Xattr);
                return Couchbase.KeyValue.LookupInSpec.Count(protoSpec.Count.Path);
            }
            case Grpc.Protocol.Sdk.Kv.LookupIn.LookupInSpec.OperationOneofCase.Exists:
            {
                if (protoSpec.Exists.HasXattr) return Couchbase.KeyValue.LookupInSpec.Exists(protoSpec.Exists.Path, protoSpec.Exists.Xattr);
                return Couchbase.KeyValue.LookupInSpec.Exists(protoSpec.Exists.Path);
            }
            default:
                throw new ArgumentException("Unknown LookupInSpec type");
        }
    }

    public static async Task<Grpc.Protocol.Run.Result> ProcessGetReplicasResult(Task<IGetReplicaResult> replicaResultTask, GetAllReplicas request)
    {
        var replicaResult = await replicaResultTask.ConfigureAwait(false);
        var result = new Grpc.Protocol.Run.Result();
        var builder = new  Couchbase.Grpc.Protocol.Sdk.Kv.GetReplicaResult
        {
            Cas = (long)replicaResult.Cas,
            Content = CommandUtils.DeserializeContentType(request.ContentAs.AsCase, replicaResult),
            IsReplica = replicaResult.IsActive,
            StreamId = request.StreamConfig.StreamId
        };

        result.Sdk = new Result();
        result.Sdk.GetReplicaResult = builder;

        return result;
    }

    public static void PopulateResult(int indexedDocCount, Couchbase.Grpc.Protocol.Run.Result result)
    {
        result.Sdk = new Result();
        result.Sdk.SearchIndexManagerResult = new Grpc.Protocol.Sdk.Search.IndexManager.Result();
        result.Sdk.SearchIndexManagerResult.IndexedDocumentCounts = indexedDocCount;
    }

    public static void PopulateResult(SearchIndex searchIndex, Couchbase.Grpc.Protocol.Run.Result result)
    {
        result.Sdk = new Result();
        result.Sdk.SearchIndexManagerResult = new Grpc.Protocol.Sdk.Search.IndexManager.Result();
        result.Sdk.SearchIndexManagerResult.Index = new Grpc.Protocol.Sdk.Search.IndexManager.SearchIndex
        {
            Uuid = searchIndex.Uuid,
            Name = searchIndex.Name,
            Type = searchIndex.Type,
            SourceUuid = searchIndex.SourceUuid,
            SourceType = searchIndex.SourceType,
            Params = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(searchIndex.Params)),
            SourceParams = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(searchIndex.SourceParams)),
            PlanParams = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(searchIndex.PlanParams))
        };
    }

    public static void PopulateResult(IEnumerable<SearchIndex> searchIndexes, Couchbase.Grpc.Protocol.Run.Result result)
    {
        result.Sdk = new Result();
        result.Sdk.SearchIndexManagerResult = new Grpc.Protocol.Sdk.Search.IndexManager.Result();
        result.Sdk.SearchIndexManagerResult.Indexes = new SearchIndexes();
        foreach (var searchIndex in searchIndexes)
        {
            result.Sdk.SearchIndexManagerResult.Indexes.Indexes.Add(new Grpc.Protocol.Sdk.Search.IndexManager.SearchIndex
            {
                Uuid = searchIndex.Uuid,
                Name = searchIndex.Name,
                Type = searchIndex.Type,
                SourceUuid = searchIndex.SourceUuid,
                SourceType = searchIndex.SourceType,
                Params = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(searchIndex.Params)),
                SourceParams = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(searchIndex.SourceParams)),
                PlanParams = ByteString.CopyFromUtf8(JsonConvert.SerializeObject(searchIndex.PlanParams))
            });
        }
    }

    public static void PopulateResult(
        IEnumerable<Couchbase.Management.Collections.ScopeSpec> scopes, Couchbase.Grpc.Protocol.Run.Result result)
    {
        result.Sdk = new Result();
        result.Sdk.CollectionManagerResult = new Grpc.Protocol.Sdk.Bucket.CollectionManager.Result();
        result.Sdk.CollectionManagerResult.GetAllScopesResult = new GetAllScopesResult();

        var builder = new RepeatedField<Couchbase.Grpc.Protocol.Sdk.Bucket.CollectionManager.ScopeSpec>();
        foreach (var scope in scopes)
        {
            var scopeSpec = new Couchbase.Grpc.Protocol.Sdk.Bucket.CollectionManager.ScopeSpec
            {
                Name = scope.Name
            };
            scopeSpec.Collections.AddRange(scope.Collections.Select(spec =>
            {
                var protoSpec = new Couchbase.Grpc.Protocol.Sdk.Bucket.CollectionManager.CollectionSpec
                {
                    Name = spec.Name,
                    ScopeName = spec.ScopeName
                };
                if (spec.MaxExpiry.HasValue) protoSpec.ExpirySecs = (int)spec.MaxExpiry.Value.TotalSeconds;
                if (spec.History.HasValue) protoSpec.History = spec.History.Value;
                return protoSpec;
            }));
            builder.Add(scopeSpec);
        }
        result.Sdk.CollectionManagerResult.GetAllScopesResult.Result.AddRange(builder);
    }

    #region BucketManager

    public static void PopulateResult(BucketSettings settings,
        Couchbase.Grpc.Protocol.Run.Result result)
    {
        result.Sdk = new Result();
        result.Sdk.BucketManagerResult = new Grpc.Protocol.Sdk.Cluster.BucketManager.Result();
        result.Sdk.BucketManagerResult.BucketSettings = ConvertBucketSettings(settings);
    }
    public static void PopulateResult(Dictionary<string, BucketSettings> settings, Couchbase.Grpc.Protocol.Run.Result result)
    {
        result.Sdk = new Result();
        result.Sdk.BucketManagerResult = new Grpc.Protocol.Sdk.Cluster.BucketManager.Result();
        result.Sdk.BucketManagerResult.GetAllBucketsResult = new GetAllBucketsResult();

        foreach (var setting in settings)
        {
            var bucketSpec = ConvertBucketSettings(setting.Value);
            result.Sdk.BucketManagerResult.GetAllBucketsResult.Result.Add(setting.Key, bucketSpec);
        }
    }

    public static Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.BucketSettings ConvertBucketSettings(BucketSettings settings)
    {
        var protoSettings = new Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.BucketSettings();

        protoSettings.Name = settings.Name;
        protoSettings.RamQuotaMB = settings.RamQuotaMB;
        protoSettings.FlushEnabled = settings.FlushEnabled;
        protoSettings.NumReplicas = settings.NumReplicas;
        protoSettings.ReplicaIndexes = settings.ReplicaIndexes;
        protoSettings.BucketType = settings.BucketType.ToProto();
        protoSettings.MaxExpirySeconds = settings.MaxTtl;
        if (settings.NumVBuckets.HasValue) protoSettings.NumVbuckets = settings.NumVBuckets.Value;
        if (settings.EvictionPolicy.HasValue) protoSettings.EvictionPolicy = settings.EvictionPolicy.ToProto();
        if (settings.CompressionMode.HasValue) protoSettings.CompressionMode = settings.CompressionMode.Value.ToProto();
        protoSettings.MinimumDurabilityLevel = settings.DurabilityMinimumLevel.ToProto();
        if (settings.StorageBackend.HasValue) protoSettings.StorageBackend = settings.StorageBackend.ToProto();
        if (settings.HistoryRetentionCollectionDefault.HasValue) protoSettings.HistoryRetentionCollectionDefault = settings.HistoryRetentionCollectionDefault.Value;
        if (settings.HistoryRetentionDuration.HasValue) protoSettings.HistoryRetentionSeconds = (int)settings.HistoryRetentionDuration.Value.TotalSeconds;
        if (settings.HistoryRetentionBytes.HasValue) protoSettings.HistoryRetentionBytes = settings.HistoryRetentionBytes.Value;

        return protoSettings;
    }

    public static Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.EvictionPolicyType ToProto(this Couchbase.Management.Buckets.EvictionPolicyType? evictionPolicy)
    {
        return evictionPolicy switch
        {
            Couchbase.Management.Buckets.EvictionPolicyType.FullEviction => Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.EvictionPolicyType.Full,
            Couchbase.Management.Buckets.EvictionPolicyType.ValueOnly => Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.EvictionPolicyType.ValueOnly,
            Couchbase.Management.Buckets.EvictionPolicyType.NoEviction => Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.EvictionPolicyType.NoEviction,
            Couchbase.Management.Buckets.EvictionPolicyType.NotRecentlyUsed => Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.EvictionPolicyType.NotRecentlyUsed,
            _ => throw new NotSupportedException("Unknown eviction policy")
        };
    }

    public static Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.BucketType ToProto(this Couchbase.Management.Buckets.BucketType bucketType)
    {
        return bucketType switch
        {
            Couchbase.Management.Buckets.BucketType.Couchbase => Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.BucketType.Couchbase,
            Couchbase.Management.Buckets.BucketType.Memcached => Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.BucketType.Memcached,
            Couchbase.Management.Buckets.BucketType.Ephemeral => Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.BucketType.Ephemeral,
            _ => throw new NotSupportedException("Unknown eviction policy")
        };
    }

    public static Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.CompressionMode ToProto(this Couchbase.Management.Buckets.CompressionMode compressionMode)
    {
        return compressionMode switch
        {
            Couchbase.Management.Buckets.CompressionMode.Off => Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.CompressionMode.Off,
            Couchbase.Management.Buckets.CompressionMode.Passive => Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.CompressionMode.Passive,
            Couchbase.Management.Buckets.CompressionMode.Active => Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.CompressionMode.Active,
            _ => throw new NotSupportedException("Unknown eviction policy")
        };
    }

    public static Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.StorageBackend ToProto(this Couchbase.Management.Buckets.StorageBackend? storageBackend)
    {
        return storageBackend switch
        {
            Couchbase.Management.Buckets.StorageBackend.Couchstore => Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.StorageBackend.Couchstore,
            Couchbase.Management.Buckets.StorageBackend.Magma => Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.StorageBackend.Magma,
            _ => throw new NotSupportedException("Unknown eviction policy")
        };
    }

    public static Couchbase.Grpc.Protocol.Shared.Durability ToProto(this Couchbase.KeyValue.DurabilityLevel durabilityLevel)
    {
        return durabilityLevel switch
        {
            Couchbase.KeyValue.DurabilityLevel.None => Couchbase.Grpc.Protocol.Shared.Durability.None,
            Couchbase.KeyValue.DurabilityLevel.Majority => Couchbase.Grpc.Protocol.Shared.Durability.Majority,
            Couchbase.KeyValue.DurabilityLevel.MajorityAndPersistToActive => Couchbase.Grpc.Protocol.Shared.Durability.MajorityAndPersistToActive,
            Couchbase.KeyValue.DurabilityLevel.PersistToMajority => Couchbase.Grpc.Protocol.Shared.Durability.PersistToMajority,
            _ => throw new NotSupportedException("Unknown eviction policy")
        };
    }

    #endregion
}