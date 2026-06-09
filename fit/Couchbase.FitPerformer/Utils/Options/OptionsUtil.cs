#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Diagnostics;
using Couchbase.Grpc.Protocol.Sdk.Collection.MutateIn;
using Couchbase.Grpc.Protocol.Sdk.Kv;
using Couchbase.Grpc.Protocol.Sdk.Kv.RangeScan;
using Couchbase.Grpc.Protocol.Sdk.Search.IndexManager;
using Couchbase.Grpc.Protocol.Shared;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Query;
using AllowQueryingSearchIndexOptions = Couchbase.Management.Search.AllowQueryingSearchIndexOptions;
using DisallowQueryingSearchIndexOptions = Couchbase.Management.Search.DisallowQueryingSearchIndexOptions;
using DropSearchIndexOptions = Couchbase.Management.Search.DropSearchIndexOptions;
using FreezePlanSearchIndexOptions = Couchbase.Management.Search.FreezePlanSearchIndexOptions;
using GetAllSearchIndexesOptions = Couchbase.Management.Search.GetAllSearchIndexesOptions;
using GetSearchIndexOptions = Couchbase.Management.Search.GetSearchIndexOptions;
using PauseIngestSearchIndexOptions = Couchbase.Management.Search.PauseIngestSearchIndexOptions;
using ResumeIngestSearchIndexOptions = Couchbase.Management.Search.ResumeIngestSearchIndexOptions;
using UnfreezePlanSearchIndexOptions = Couchbase.Management.Search.UnfreezePlanSearchIndexOptions;
using UpsertSearchIndexOptions = Couchbase.Management.Search.UpsertSearchIndexOptions;
using GetSearchIndexDocumentCountOptions = Couchbase.Management.Search.GetSearchIndexDocumentCountOptions;

using ReadPreference = Couchbase.Grpc.Protocol.Shared.ReadPreference;

namespace Couchbase.FitPerformer.Utils.Options;

public static class OptionsUtil
{
    public static QueryOptions ConvertQueryOptions(Couchbase.Grpc.Protocol.Sdk.Query.QueryOptions? protoOptions, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        var options = new QueryOptions();

        if (protoOptions != null)
        {
            if (protoOptions.HasAdhoc) options.AdHoc(protoOptions.Adhoc);
            if (protoOptions.HasMetrics) options.Metrics(protoOptions.Metrics);
            if (protoOptions.HasProfile) options.Profile(ConvertQueryProfile(protoOptions.Profile));
            if (protoOptions.HasReadonly) options.Readonly(protoOptions.Readonly);
            if (protoOptions.HasFlexIndex) options.FlexIndex(protoOptions.FlexIndex);
            if (protoOptions.HasMaxParallelism) options.MaxServerParallelism(protoOptions.MaxParallelism);
            if (protoOptions.HasPipelineBatch) options.PipelineBatch(protoOptions.PipelineBatch);
            if (protoOptions.HasPipelineCap) options.PipelineCap(protoOptions.PipelineCap);
            if (protoOptions.HasScanCap) options.ScanCap(protoOptions.ScanCap);
            if (protoOptions.HasScanConsistency) options.ScanConsistency(ConvertScanConsistency(protoOptions.ScanConsistency));
            if (protoOptions.HasTimeoutMillis) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMillis));
            if (protoOptions.HasParentSpanId) options.RequestSpan(spans[protoOptions.ParentSpanId]);
            if (protoOptions.HasScanWaitMillis) options.ScanWait(TimeSpan.FromMilliseconds(protoOptions.ScanWaitMillis));
            if (protoOptions.HasClientContextId) options.ClientContextId(protoOptions.ClientContextId);
            if (protoOptions.ConsistentWith != null) options.ConsistentWith(ConvertMutationState(protoOptions.ConsistentWith));
            if (protoOptions.HasPreserveExpiry) options.PreserveExpiry(protoOptions.PreserveExpiry);
            if (protoOptions.HasUseReplica) options.UseReplica(protoOptions.UseReplica);
            protoOptions.ParametersNamed?.ToList().ForEach(kvp => options.Parameter(kvp.Key, kvp.Value));
            protoOptions.ParametersPositional?.ToList().ForEach(x => options.Parameter(x));
            protoOptions.Raw?.ToList().ForEach(kvp => options.Raw(kvp.Key, kvp.Value));
        }

        return options;
    }

    public static ConflictResolutionType ConvertConflictResolutionType(Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.ConflictResolutionType protoConflictResolution)
    {
        switch (protoConflictResolution)
        {
            case Grpc.Protocol.Sdk.Cluster.BucketManager.ConflictResolutionType.Custom:
                return ConflictResolutionType.Custom;
            case Grpc.Protocol.Sdk.Cluster.BucketManager.ConflictResolutionType.SequenceNumber:
                return ConflictResolutionType.SequenceNumber;
            case Grpc.Protocol.Sdk.Cluster.BucketManager.ConflictResolutionType.Timestamp:
                return ConflictResolutionType.Timestamp;
            default:
                throw new NotSupportedException("Could not convert ConflictResolutionType.");
        }
    }

    public static QueryScanConsistency ConvertScanConsistency(Couchbase.Grpc.Protocol.Shared.ScanConsistency protoConsistency)
    {
        switch (protoConsistency)
        {
            case ScanConsistency.NotBounded:
                return QueryScanConsistency.NotBounded;
            case ScanConsistency.RequestPlus:
                return QueryScanConsistency.RequestPlus;
            default:
                throw new NotSupportedException("Could not convert ScanConsistency.");
        }
    }

    public static ITypeTranscoder GetTranscoder(Transcoder.TranscoderOneofCase? requestTranscoderOneofCase)
    {
        switch (requestTranscoderOneofCase)
        {
            case Transcoder.TranscoderOneofCase.RawBinary:
                return new Couchbase.Core.IO.Transcoders.RawBinaryTranscoder();
            case Transcoder.TranscoderOneofCase.Json:
                return new Couchbase.Core.IO.Transcoders.JsonTranscoder();
                break;
            case Transcoder.TranscoderOneofCase.RawJson:
                var transcoder = new Couchbase.Core.IO.Transcoders.RawJsonTranscoder
                {
                    Serializer = new DefaultSerializer()
                };
                return transcoder;
            case Transcoder.TranscoderOneofCase.RawString:
               return new Core.IO.Transcoders.RawStringTranscoder();
            default:
                return new Core.IO.Transcoders.JsonTranscoder();
        }
    }

    private static QueryProfile ConvertQueryProfile(string protoProfile)
    {
        switch (protoProfile)
        {
            case "off":
                return QueryProfile.Off;
            case "phases":
                return QueryProfile.Phases;
            case "timings":
                return QueryProfile.Timings;
            default:
                throw new NotSupportedException("Could not convert QueryProfile");
        }
    }

    public static KeyValue.GetAndTouchOptions? CreateOptions(GetAndTouch request, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (request.Options == null) return null;
        var opts = request.Options;
        var ret = new Couchbase.KeyValue.GetAndTouchOptions();
        if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
        if (opts.Transcoder != null) ret.Transcoder(ConvertTranscoder(opts.Transcoder));
        if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
        return ret;
    }

    public static KeyValue.TouchOptions? CreateOptions(Touch request, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (request.Options == null) return null;
        var opts = request.Options;
        var ret = new Couchbase.KeyValue.TouchOptions();
        if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
        if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
        return ret;
    }

    public static KeyValue.GetAndLockOptions? CreateOptions(GetAndLock request, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (request.Options == null) return null;
        var opts = request.Options;
        var ret = new Couchbase.KeyValue.GetAndLockOptions();
        if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
        if (opts.Transcoder != null) ret.Transcoder(ConvertTranscoder(opts.Transcoder));
        if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
        return ret;
    }

    public static KeyValue.UnlockOptions? CreateOptions(Unlock request, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (request.Options == null) return null;
        var opts = request.Options;
        var ret = new Couchbase.KeyValue.UnlockOptions();
        if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
        if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
        return ret;
    }

    public static KeyValue.ExistsOptions? CreateOptions(Exists request, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (request.Options == null) return null;
        var opts = request.Options;
        var ret = new Couchbase.KeyValue.ExistsOptions();
        if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
        if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
        return ret;
    }

    public static KeyValue.MutateInOptions? CreateOptions(MutateIn request, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (request.Options == null) return null;
        var opts = request.Options;
        var ret = new Couchbase.KeyValue.MutateInOptions();
        if (opts.HasTimeoutMillis) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMillis));
        if (opts.HasCas) ret.Cas((ulong)opts.Cas);
        if (opts.HasStoreSemantics) ret.StoreSemantics(CommandUtils.ConvertStoreSemantics(opts.StoreSemantics));
        if (opts.HasAccessDeleted) ret.AccessDeleted(opts.AccessDeleted);
        if (opts.Expiry != null)
        {
            ret.Expiry(CommandUtils.ConvertExpiry(opts.Expiry));
            Console.WriteLine(DateTimeOffset.FromUnixTimeSeconds(opts.Expiry.AbsoluteEpochSecs));
            Console.WriteLine();
        }
        if (opts.HasPreserveExpiry) ret.PreserveTtl(opts.PreserveExpiry);
        if (opts.HasCreateAsDeleted) ret.CreateAsDeleted(opts.CreateAsDeleted);
        if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
        return ret;
    }

    public static KeyValue.GetAnyReplicaOptions? CreateOptions(GetAnyReplica request, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (request.Options == null) return null;
        var opts = request.Options;
        var ret = new Couchbase.KeyValue.GetAnyReplicaOptions();
        // TODO does .net not support timeout for GetAnyReplicaOptions?
        //if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
        if (opts.Transcoder != null) ret.Transcoder(ConvertTranscoder(opts.Transcoder));
        if (opts.HasReadPreference) ret.ReadPreference(opts.ReadPreference.ConvertReadPreference());
        if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
        return ret;
    }

    public static KeyValue.GetAllReplicasOptions? CreateOptions(GetAllReplicas request, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (request.Options == null) return null;
        var opts = request.Options;
        var ret = new Couchbase.KeyValue.GetAllReplicasOptions();
        // TODO does .net not support timeout for GetAllReplicasOptions?
        // if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
        if (opts.Transcoder != null) ret.Transcoder(ConvertTranscoder(opts.Transcoder));
        if (opts.HasReadPreference) ret.ReadPreference(opts.ReadPreference.ConvertReadPreference());
        if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
        return ret;
    }

    public static KeyValue.AppendOptions? CreateOptions(Append request, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (request.Options == null) return null;
        var opts = request.Options;
        var ret = new Couchbase.KeyValue.AppendOptions();
        if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
        if (opts.HasCas) ret.Cas((ulong)opts.Cas);
        if (opts.Durability != null) DurabilityUtil.ConvertDurability(opts.Durability, ret);
        if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
        return ret;
    }

    public static KeyValue.PrependOptions? CreateOptions(Prepend request, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (request.Options == null) return null;
        var opts = request.Options;
        var ret = new Couchbase.KeyValue.PrependOptions();
        if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
        if (opts.HasCas) ret.Cas((ulong)opts.Cas);
        if (opts.Durability != null) DurabilityUtil.ConvertDurability(opts.Durability, ret);
        if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
        return ret;
    }

    public static KeyValue.IncrementOptions? CreateOptions(Increment request, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (request.Options == null) return null;
        var opts = request.Options;
        var ret = new Couchbase.KeyValue.IncrementOptions();
        if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
        if (opts.HasDelta) ret.Delta((ulong)opts.Delta);
        if (opts.HasInitial) ret.Initial((ulong)opts.Initial);
        if (opts.Durability != null) DurabilityUtil.ConvertDurability(opts.Durability, ret);
        if (opts.Expiry != null) ret.Expiry(CommandUtils.ConvertExpiry(opts.Expiry));
        if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
        return ret;
    }

    public static KeyValue.DecrementOptions? CreateOptions(Decrement request, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        if (request.Options == null) return null;
        var opts = request.Options;
        var ret = new Couchbase.KeyValue.DecrementOptions();
        if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
        if (opts.HasDelta) ret.Delta((ulong)opts.Delta);
        if (opts.HasInitial) ret.Initial((ulong)opts.Initial);
        if (opts.Durability != null) DurabilityUtil.ConvertDurability(opts.Durability, ret);
        if (opts.Expiry != null) ret.Expiry(CommandUtils.ConvertExpiry(opts.Expiry));
        if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
        return ret;
    }

    public static Management.Query.CreateQueryIndexOptions? ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Query.IndexManager.CreateQueryIndexOptions? protoOptions)
    {
        if (protoOptions != null)
        {
            var options = new Management.Query.CreateQueryIndexOptions();
            if (protoOptions.HasIgnoreIfExists) options.IgnoreIfExists(protoOptions.IgnoreIfExists);
            if (protoOptions.HasDeferred) options.Deferred(protoOptions.Deferred);
            if (protoOptions.HasScopeName) options.ScopeName(protoOptions.ScopeName);
            if (protoOptions.HasCollectionName) options.CollectionName(protoOptions.CollectionName);
            options.CancellationToken(new CancellationToken());

            return options;
        }
        return Couchbase.Management.Query.CreateQueryIndexOptions.Default;
    }

    public static Management.Query.DropQueryIndexOptions? ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Query.IndexManager.DropIndexOptions? protoOptions)
    {
        if (protoOptions != null)
        {
            var options = new Management.Query.DropQueryIndexOptions();
            if (protoOptions.HasIgnoreIfNotExists) options.IgnoreIfExists(!protoOptions.IgnoreIfNotExists);
            if (protoOptions.HasScopeName) options.ScopeName(protoOptions.ScopeName);
            if (protoOptions.HasCollectionName) options.CollectionName(protoOptions.CollectionName);
            options.CancellationToken(new CancellationToken());

            return options;
        }
        return Couchbase.Management.Query.DropQueryIndexOptions.Default;
    }

    public static Management.Query.WatchQueryIndexOptions? ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Query.IndexManager.WatchIndexesOptions? protoOptions)
    {
        if (protoOptions != null)
        {
            var options = new Management.Query.WatchQueryIndexOptions();
            if (protoOptions.HasWatchPrimary) options.WatchPrimary(protoOptions.WatchPrimary);
            if (protoOptions.HasScopeName) options.ScopeName(protoOptions.ScopeName);
            if (protoOptions.HasCollectionName) options.CollectionName(protoOptions.CollectionName);
            options.CancellationToken(new CancellationToken());

            return options;
        }
        return Couchbase.Management.Query.WatchQueryIndexOptions.Default;
    }

    public static Management.Query.BuildDeferredQueryIndexOptions? ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Query.IndexManager.BuildDeferredIndexesOptions? protoOptions)
    {
        if (protoOptions != null)
        {
            var options = new Management.Query.BuildDeferredQueryIndexOptions();
            if (protoOptions.HasScopeName) options.ScopeName(protoOptions.ScopeName);
            if (protoOptions.HasCollectionName) options.CollectionName(protoOptions.CollectionName);
            options.CancellationToken(new CancellationToken());

            return options;
        }
        return Couchbase.Management.Query.BuildDeferredQueryIndexOptions.Default;
    }

    public static Management.Query.CreatePrimaryQueryIndexOptions? ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Query.IndexManager.CreatePrimaryQueryIndexOptions? protoOptions)
    {
        if (protoOptions != null)
        {
            var options = new Management.Query.CreatePrimaryQueryIndexOptions();
            if (protoOptions.HasIndexName) options.IndexName(protoOptions.IndexName);
            if (protoOptions.HasIgnoreIfExists) options.IgnoreIfExists(protoOptions.IgnoreIfExists);
            if (protoOptions.HasDeferred) options.Deferred(protoOptions.Deferred);
            if (protoOptions.HasScopeName) options.ScopeName(protoOptions.ScopeName);
            if (protoOptions.HasCollectionName) options.CollectionName(protoOptions.CollectionName);
            options.CancellationToken(new CancellationToken());

            return options;
        }
        return Couchbase.Management.Query.CreatePrimaryQueryIndexOptions.Default;
    }
    public static Management.Query.DropPrimaryQueryIndexOptions? ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Query.IndexManager.DropPrimaryIndexOptions? protoOptions)
    {
        if (protoOptions != null)
        {
            var options = new Management.Query.DropPrimaryQueryIndexOptions();
            if (protoOptions.HasIgnoreIfNotExists) options.IgnoreIfExists(!protoOptions.IgnoreIfNotExists);
            if (protoOptions.HasScopeName) options.ScopeName(protoOptions.ScopeName);
            if (protoOptions.HasCollectionName) options.CollectionName(protoOptions.CollectionName);
            options.CancellationToken(new CancellationToken());

            return options;
        }
        return Couchbase.Management.Query.DropPrimaryQueryIndexOptions.Default;
    }
    public static Management.Query.GetAllQueryIndexOptions? ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Query.IndexManager.GetAllQueryIndexOptions? protoOptions)
    {
        if (protoOptions != null)
        {
            var options = new Management.Query.GetAllQueryIndexOptions();
            if (protoOptions.HasScopeName) options.ScopeName(protoOptions.ScopeName);
            if (protoOptions.HasCollectionName) options.CollectionName(protoOptions.CollectionName);
            options.CancellationToken(new CancellationToken());

            return options;
        }
        return Couchbase.Management.Query.GetAllQueryIndexOptions.Default;
    }

    public static WaitUntilReadyOptions ConvertWaitUntilReadyOptions(Couchbase.Grpc.Protocol.Sdk.Cluster.WaitUntilReady.WaitUntilReadyOptions? protoOptions)
    {
         var options = new WaitUntilReadyOptions();

         if (protoOptions != null)
         {
             if (protoOptions.HasDesiredState) options.DesiredState(ConvertClusterState(protoOptions.DesiredState));
             if (protoOptions.ServiceTypes.Count > 0) options.ServiceTypes(ConvertServiceTypes(protoOptions.ServiceTypes.ToList()));
         }

         return options;
    }

    private static ServiceType[] ConvertServiceTypes(List<Couchbase.Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ServiceType> protoServiceType)
    {
        var serviceTypes = new List<ServiceType>(protoServiceType.Count);
        foreach (var service in protoServiceType)
        {
            switch (service)
            {
                case Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ServiceType.Analytics:
                    serviceTypes.Add(ServiceType.Analytics);
                    break;
                case Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ServiceType.Backup:
                    throw new ArgumentException("No Backup ServiceType in SDK enum");
                case Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ServiceType.Eventing:
                    serviceTypes.Add(ServiceType.Eventing);
                    break;
                case Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ServiceType.Kv:
                    serviceTypes.Add(ServiceType.KeyValue);
                    break;
                case Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ServiceType.Manager:
                    serviceTypes.Add(ServiceType.Management);
                    break;
                case Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ServiceType.Query:
                    serviceTypes.Add(ServiceType.Query);
                    break;
                case Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ServiceType.Search:
                    serviceTypes.Add(ServiceType.Search);
                    break;
                case Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ServiceType.Views:
                    serviceTypes.Add(ServiceType.Views);
                    break;
                default:
                    throw new ArgumentException("Unknown value for ServiceType enum.");
            }
        }

        return serviceTypes.ToArray();
    }

    private static ClusterState ConvertClusterState(Couchbase.Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ClusterState protoState)
    {
        switch (protoState)
        {
            case Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ClusterState.Degraded:
                return ClusterState.Degraded;
            case Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ClusterState.Offline:
                return ClusterState.Offline;
            case Grpc.Protocol.Sdk.Cluster.WaitUntilReady.ClusterState.Online:
                return ClusterState.Online;
            default:
                throw new ArgumentException("Unknown value for ClusterState enum.");
        }
    }

    public static LookupInOptions ConvertLookupInOptions(Couchbase.Grpc.Protocol.Sdk.Kv.LookupIn.LookupInOptions? protoOptions)
    {
        var options = new LookupInOptions();

        if (protoOptions != null)
        {
            if (protoOptions.HasAccessDeleted) options.AccessDeleted(protoOptions.AccessDeleted);
            if (protoOptions.HasTimeoutMillis) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMillis));
        }

        return options;
    }

    #region SearchIndexManagement
    //Note: Lengthy and repetitive... but the generated classes don't share any inheritance.
    public static AllowQueryingSearchIndexOptions ConvertSearchIndexManagementOptions(Couchbase.Grpc.Protocol.Sdk.Search.IndexManager.AllowQueryingSearchIndexOptions? protoOptions)
    {
        var options = new AllowQueryingSearchIndexOptions();

        if (protoOptions != null)
        {
            // if (protoOptions.HasParentSpanId) : No Span support in SDK
            if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
        }

        return options;
    }

    public static DisallowQueryingSearchIndexOptions ConvertSearchIndexManagementOptions(Couchbase.Grpc.Protocol.Sdk.Search.IndexManager.DisallowQueryingSearchIndexOptions? protoOptions)
    {
        var options = new DisallowQueryingSearchIndexOptions();

        if (protoOptions != null)
        {
            // if (protoOptions.HasParentSpanId) : No Span support in SDK
            if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
        }

        return options;
    }

    public static DropSearchIndexOptions ConvertSearchIndexManagementOptions(Couchbase.Grpc.Protocol.Sdk.Search.IndexManager.DropSearchIndexOptions? protoOptions)
    {
        var options = new DropSearchIndexOptions();

        if (protoOptions != null)
        {
            // if (protoOptions.HasParentSpanId) : No Span support in SDK
            if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
        }

        return options;
    }

    public static FreezePlanSearchIndexOptions ConvertSearchIndexManagementOptions(Couchbase.Grpc.Protocol.Sdk.Search.IndexManager.FreezePlanSearchIndexOptions? protoOptions)
    {
        var options = new FreezePlanSearchIndexOptions();

        if (protoOptions != null)
        {
            // if (protoOptions.HasParentSpanId) : No Span support in SDK
            if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
        }

        return options;
    }

    public static UnfreezePlanSearchIndexOptions ConvertSearchIndexManagementOptions(Couchbase.Grpc.Protocol.Sdk.Search.IndexManager.UnfreezePlanSearchIndexOptions? protoOptions)
    {
        var options = new UnfreezePlanSearchIndexOptions();

        if (protoOptions != null)
        {
            // if (protoOptions.HasParentSpanId) : No Span support in SDK
            if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
        }

        return options;
    }

    public static GetSearchIndexOptions ConvertSearchIndexManagementOptions(Couchbase.Grpc.Protocol.Sdk.Search.IndexManager.GetSearchIndexOptions? protoOptions)
    {
        var options = new GetSearchIndexOptions();

        if (protoOptions != null)
        {
            // if (protoOptions.HasParentSpanId) : No Span support in SDK
            if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
        }

        return options;
    }

    public static PauseIngestSearchIndexOptions ConvertSearchIndexManagementOptions(Couchbase.Grpc.Protocol.Sdk.Search.IndexManager.PauseIngestSearchIndexOptions? protoOptions)
    {
        var options = new PauseIngestSearchIndexOptions();

        if (protoOptions != null)
        {
            // if (protoOptions.HasParentSpanId) : No Span support in SDK
            if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
        }

        return options;
    }

    public static ResumeIngestSearchIndexOptions ConvertSearchIndexManagementOptions(Couchbase.Grpc.Protocol.Sdk.Search.IndexManager.ResumeIngestSearchIndexOptions? protoOptions)
    {
        var options = new ResumeIngestSearchIndexOptions();

        if (protoOptions != null)
        {
            // if (protoOptions.HasParentSpanId) : No Span support in SDK
            if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
        }

        return options;
    }

    public static UpsertSearchIndexOptions ConvertSearchIndexManagementOptions(Couchbase.Grpc.Protocol.Sdk.Search.IndexManager.UpsertSearchIndexOptions? protoOptions)
    {
        var options = new UpsertSearchIndexOptions();

        if (protoOptions != null)
        {
            // if (protoOptions.HasParentSpanId) : No Span support in SDK
            if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
        }

        return options;
    }

    public static GetAllSearchIndexesOptions ConvertSearchIndexManagementOptions(Couchbase.Grpc.Protocol.Sdk.Search.IndexManager.GetAllSearchIndexesOptions? protoOptions)
    {
        var options = new GetAllSearchIndexesOptions();

        if (protoOptions != null)
        {
            // if (protoOptions.HasParentSpanId) : No Span support in SDK
            if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
        }

        return options;
    }

    public static GetSearchIndexDocumentCountOptions ConvertSearchIndexManagementOptions(Couchbase.Grpc.Protocol.Sdk.Search.IndexManager.GetIndexedSearchIndexOptions? protoOptions)
    {
        var options = new GetSearchIndexDocumentCountOptions();

        if (protoOptions != null)
        {
            // if (protoOptions.HasParentSpanId) : No Span support in SDK
            if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
        }

        return options;
    }
    #endregion


    #region LookupInReplicas
    public static Couchbase.KeyValue.LookupInAnyReplicaOptions ConvertLookupInAnyReplicasOptions(Couchbase.Grpc.Protocol.Sdk.Kv.LookupIn.LookupInAnyReplicaOptions? protoOptions, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        var options = new Couchbase.KeyValue.LookupInAnyReplicaOptions();

        if (protoOptions != null)
        {
            if (protoOptions.HasParentSpanId) options.RequestSpan(spans[protoOptions.ParentSpanId]);
            if (protoOptions.HasTimeoutMillis) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMillis));
            if (protoOptions.HasReadPreference) options.ReadPreference(protoOptions.ReadPreference.ConvertReadPreference());
        }

        return options;
    }

    public static LookupInAllReplicasOptions ConvertLookupInAllReplicasOptions(Couchbase.Grpc.Protocol.Sdk.Kv.LookupIn.LookupInAllReplicasOptions? protoOptions, ConcurrentDictionary<string, IRequestSpan> spans)
    {
        var options = new LookupInAllReplicasOptions();

        if (protoOptions != null)
        {
            if (protoOptions.HasParentSpanId) options.RequestSpan(spans[protoOptions.ParentSpanId]);
            if (protoOptions.HasTimeoutMillis)
                options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMillis));
            if (protoOptions.HasReadPreference) options.ReadPreference(protoOptions.ReadPreference.ConvertReadPreference());
        }

        return options;
    }
    #endregion

    public static Couchbase.Query.MutationState ConvertMutationState(Couchbase.Grpc.Protocol.Shared.MutationState consistentWith)
        {
            var grpcTokens = consistentWith.Tokens;
            var cbTokens = grpcTokens.Select(x => new Couchbase.Core.MutationToken(x.BucketName, (short)x.PartitionId, x.PartitionUuid, x.SequenceNumber));
            IMutationResult[] tokenWrapper = cbTokens.Select(x => new MutationStateTokenWrapper(x)).ToArray();
            var ret = Couchbase.Query.MutationState.From(tokenWrapper);
            return ret;
        }

        private class MutationStateTokenWrapper : IMutationResult
        {
            public ulong Cas { get; }
            public Couchbase.Core.MutationToken MutationToken { get; set; }
            public MutationStateTokenWrapper(Couchbase.Core.MutationToken token)
            {
                MutationToken = token;
            }
        }

        public static Couchbase.KeyValue.InsertOptions? CreateOptions(Couchbase.Grpc.Protocol.Sdk.Kv.Insert request, ConcurrentDictionary<string, IRequestSpan> spans)
        {
            if (request.Options != null)
            {
                var opts = request.Options;
                var ret = new Couchbase.KeyValue.InsertOptions();
                if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
                if (opts.Durability != null) ConvertDurability(opts.Durability, ret);
                if (opts.Expiry != null)
                {
                    if (opts.Expiry.ExpiryTypeCase == Expiry.ExpiryTypeOneofCase.RelativeSecs) ret.Expiry(TimeSpan.FromSeconds(opts.Expiry.RelativeSecs));
                    else if (opts.Expiry.ExpiryTypeCase == Expiry.ExpiryTypeOneofCase.AbsoluteEpochSecs) throw new NotSupportedException();
                    else throw new NotSupportedException("Unknown expiry");
                }
                if (opts.Transcoder != null) ret.Transcoder(ConvertTranscoder(opts.Transcoder));
                if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
                return ret;
            }
            else return null;
        }

        public static Couchbase.KeyValue.RemoveOptions? CreateOptions(Couchbase.Grpc.Protocol.Sdk.Kv.Remove request, ConcurrentDictionary<string, IRequestSpan> spans)
        {
            if (request.Options != null)
            {
                var opts = request.Options;
                var ret = new Couchbase.KeyValue.RemoveOptions();
                if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
                if (opts.Durability != null) ConvertDurability(opts.Durability, ret);
                if (opts.HasCas) ret.Cas((ulong)opts.Cas);
                if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
                return ret;
            }
            else return null;
        }

        public static Couchbase.KeyValue.GetOptions? CreateOptions(Couchbase.Grpc.Protocol.Sdk.Kv.Get request, ConcurrentDictionary<string, IRequestSpan> spans)
        {
            if (request.Options != null)
            {
                var opts = request.Options;
                var ret = new Couchbase.KeyValue.GetOptions();
                if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
                if (opts.HasWithExpiry && opts.WithExpiry) ret.Expiry();
                if (opts.Projection.Count > 0) ret.Projection(opts.Projection.Select(v => v.ToString()).ToArray());
                if (opts.Transcoder != null) ret.Transcoder(ConvertTranscoder(opts.Transcoder));
                if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
                return ret;
            }
            else return null;
        }

        public static Couchbase.KeyValue.ReplaceOptions? CreateOptions(Couchbase.Grpc.Protocol.Sdk.Kv.Replace request, ConcurrentDictionary<string, IRequestSpan> spans)
        {
            if (request.Options != null)
            {
                var opts = request.Options;
                var ret = new Couchbase.KeyValue.ReplaceOptions();
                if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
                if (opts.Durability != null) ConvertDurability(opts.Durability, ret);
                if (opts.Expiry != null)
                {
                    if (opts.Expiry.ExpiryTypeCase == Expiry.ExpiryTypeOneofCase.RelativeSecs) ret.Expiry(TimeSpan.FromSeconds(opts.Expiry.RelativeSecs));
                    else if (opts.Expiry.ExpiryTypeCase == Expiry.ExpiryTypeOneofCase.AbsoluteEpochSecs) throw new NotSupportedException();
                    else throw new NotSupportedException("Unknown expiry");
                }
                if (opts.HasPreserveExpiry) ret.PreserveTtl(opts.PreserveExpiry);
                if (opts.HasCas) ret.Cas((ulong)opts.Cas);
                if (opts.Transcoder != null) ret.Transcoder(ConvertTranscoder(opts.Transcoder));
                if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
                return ret;
            }
            else return null;
        }

        public static Couchbase.KeyValue.UpsertOptions? CreateOptions(Couchbase.Grpc.Protocol.Sdk.Kv.Upsert request, ConcurrentDictionary<string, IRequestSpan> spans)
        {
            if (request.Options != null)
            {
                var opts = request.Options;
                var ret = new Couchbase.KeyValue.UpsertOptions();
                if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
                if (opts.Durability != null) ConvertDurability(opts.Durability, ret);
                if (opts.Expiry != null)
                {
                    if (opts.Expiry.ExpiryTypeCase == Expiry.ExpiryTypeOneofCase.RelativeSecs) ret.Expiry(TimeSpan.FromSeconds(opts.Expiry.RelativeSecs));
                    else if (opts.Expiry.ExpiryTypeCase == Expiry.ExpiryTypeOneofCase.AbsoluteEpochSecs) throw new NotSupportedException();
                    else throw new NotSupportedException("Unknown expiry");
                }
                if (opts.HasPreserveExpiry) ret.PreserveTtl(opts.PreserveExpiry);
                if (opts.Transcoder != null) ret.Transcoder(ConvertTranscoder(opts.Transcoder));
                if (opts.HasParentSpanId) ret.RequestSpan(spans[opts.ParentSpanId]);
                return ret;
            }
            else return null;
        }

        public static Couchbase.KeyValue.DurabilityLevel ConvertDurabilityLevel(Couchbase.Grpc.Protocol.Shared.Durability dl)
        {
            switch (dl)
            {
                case Couchbase.Grpc.Protocol.Shared.Durability.None: return Couchbase.KeyValue.DurabilityLevel.None;
                case Couchbase.Grpc.Protocol.Shared.Durability.Majority: return Couchbase.KeyValue.DurabilityLevel.Majority;
                case Couchbase.Grpc.Protocol.Shared.Durability.MajorityAndPersistToActive: return Couchbase.KeyValue.DurabilityLevel.MajorityAndPersistToActive;
                case Couchbase.Grpc.Protocol.Shared.Durability.PersistToMajority: return Couchbase.KeyValue.DurabilityLevel.PersistToMajority;
                default: throw new NotSupportedException();
            }
        }

        public static Couchbase.KeyValue.ReplicateTo ConvertReplicateTo(Couchbase.Grpc.Protocol.Shared.ReplicateTo dl)
        {
            switch (dl)
            {
                case Couchbase.Grpc.Protocol.Shared.ReplicateTo.None: return Couchbase.KeyValue.ReplicateTo.None;
                case Couchbase.Grpc.Protocol.Shared.ReplicateTo.One: return Couchbase.KeyValue.ReplicateTo.One;
                case Couchbase.Grpc.Protocol.Shared.ReplicateTo.Two: return Couchbase.KeyValue.ReplicateTo.Two;
                case Couchbase.Grpc.Protocol.Shared.ReplicateTo.Three: return Couchbase.KeyValue.ReplicateTo.Three;
                default: throw new NotSupportedException();
            }
        }

        public static Couchbase.KeyValue.PersistTo ConvertPersistTo(Couchbase.Grpc.Protocol.Shared.PersistTo dl)
        {
            switch (dl)
            {
                case Couchbase.Grpc.Protocol.Shared.PersistTo.None: return Couchbase.KeyValue.PersistTo.None;
                case Couchbase.Grpc.Protocol.Shared.PersistTo.One: return Couchbase.KeyValue.PersistTo.One;
                case Couchbase.Grpc.Protocol.Shared.PersistTo.Two: return Couchbase.KeyValue.PersistTo.Two;
                case Couchbase.Grpc.Protocol.Shared.PersistTo.Three: return Couchbase.KeyValue.PersistTo.Three;
                case Couchbase.Grpc.Protocol.Shared.PersistTo.Four: return Couchbase.KeyValue.PersistTo.Four;
                case Couchbase.Grpc.Protocol.Shared.PersistTo.Active: throw new NotSupportedException();
                default: throw new NotSupportedException();
            }
        }

        public static void ConvertDurability(DurabilityType durability, Couchbase.KeyValue.InsertOptions options)
        {
            if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.DurabilityLevel) options.Durability(ConvertDurabilityLevel(durability.DurabilityLevel));
            else if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.Observe) options.Durability(ConvertPersistTo(durability.Observe.PersistTo), ConvertReplicateTo(durability.Observe.ReplicateTo));
            else throw new NotSupportedException("Unknown durability");
        }

        public static void ConvertDurability(DurabilityType durability, Couchbase.KeyValue.UpsertOptions options)
        {
            if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.DurabilityLevel) options.Durability(ConvertDurabilityLevel(durability.DurabilityLevel));
            else if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.Observe) options.Durability(ConvertPersistTo(durability.Observe.PersistTo), ConvertReplicateTo(durability.Observe.ReplicateTo));
            else throw new NotSupportedException("Unknown durability");
        }

        public static void ConvertDurability(DurabilityType durability, Couchbase.KeyValue.ReplaceOptions options)
        {
            if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.DurabilityLevel) options.Durability(ConvertDurabilityLevel(durability.DurabilityLevel));
            else if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.Observe) options.Durability(ConvertPersistTo(durability.Observe.PersistTo), ConvertReplicateTo(durability.Observe.ReplicateTo));
            else throw new NotSupportedException("Unknown durability");
        }

        public static void ConvertDurability(DurabilityType durability, Couchbase.KeyValue.RemoveOptions options)
        {
            if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.DurabilityLevel) options.Durability(ConvertDurabilityLevel(durability.DurabilityLevel));
            else if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.Observe) options.Durability(ConvertPersistTo(durability.Observe.PersistTo), ConvertReplicateTo(durability.Observe.ReplicateTo));
            else throw new NotSupportedException("Unknown durability");
        }

        public static Couchbase.Core.IO.Transcoders.ITypeTranscoder ConvertTranscoder(Transcoder transcoder)
        {
            if (transcoder.TranscoderCase == Transcoder.TranscoderOneofCase.RawJson) return new Couchbase.Core.IO.Transcoders.RawJsonTranscoder();
            if (transcoder.TranscoderCase == Transcoder.TranscoderOneofCase.Json) return new Couchbase.Core.IO.Transcoders.JsonTranscoder();
            if (transcoder.TranscoderCase == Transcoder.TranscoderOneofCase.Legacy) return new Couchbase.Core.IO.Transcoders.LegacyTranscoder();
            if (transcoder.TranscoderCase == Transcoder.TranscoderOneofCase.RawString) return new Couchbase.Core.IO.Transcoders.RawStringTranscoder();
            if (transcoder.TranscoderCase == Transcoder.TranscoderOneofCase.RawBinary) return new Couchbase.Core.IO.Transcoders.RawBinaryTranscoder();
            throw new NotSupportedException();
        }

        public static Couchbase.KeyValue.ZoneAware.ReadPreference ConvertReadPreference(this Couchbase.Grpc.Protocol.Shared.ReadPreference readPreference)
        {
            return readPreference switch
            {
                ReadPreference.NoPreference => Couchbase.KeyValue.ZoneAware.ReadPreference.NoPreference,
                ReadPreference.SelectedServerGroup => Couchbase.KeyValue.ZoneAware.ReadPreference.SelectedServerGroup,
                ReadPreference.SelectedServerGroupOrAllAvailable => throw new NotSupportedException(
                    "SelectedServerGroupOrAllAvailable is not supported in the .NET SDK"),
                _ => throw new NotSupportedException()
            };
        }

        #region RangeScan
        public static Couchbase.KeyValue.RangeScan.ScanType ConvertScanType(Scan request)
        {
            if (request.ScanType.TypeCase == Couchbase.Grpc.Protocol.Sdk.Kv.RangeScan.ScanType.TypeOneofCase.Range)
            {
                var rs = request.ScanType.Range;
                if (rs.RangeCase == Couchbase.Grpc.Protocol.Sdk.Kv.RangeScan.RangeScan.RangeOneofCase.FromTo)
                {
                    var from = ConvertScanTerm(rs.FromTo.From);
                    var to = ConvertScanTerm(rs.FromTo.To);

                    if (from != null && to != null)
                    {
                        return new Couchbase.KeyValue.RangeScan.RangeScan(from, to);
                    }

                    if (from == null && to == null)
                    {
                        return new Couchbase.KeyValue.RangeScan.RangeScan(from, to);
                    }

                    if (to == null)
                    {
                        return new Couchbase.KeyValue.RangeScan.RangeScan(from);
                    }

                    return new Couchbase.KeyValue.RangeScan.RangeScan(from, to);
                }
                else if (rs.RangeCase == Couchbase.Grpc.Protocol.Sdk.Kv.RangeScan.RangeScan.RangeOneofCase.DocIdPrefix)
                {
                    return new Couchbase.KeyValue.RangeScan.PrefixScan(rs.DocIdPrefix);
                }
            }

            if (request.ScanType.TypeCase == Couchbase.Grpc.Protocol.Sdk.Kv.RangeScan.ScanType.TypeOneofCase.Sampling)
            {
                var ss = request.ScanType.Sampling;
                if (ss.HasSeed)
                {
                    return new Couchbase.KeyValue.RangeScan.SamplingScan(ss.Limit, ss.Seed);
                }

                return new Couchbase.KeyValue.RangeScan.SamplingScan(ss.Limit);
            }

            throw new UnsupportedException("Unknown ScanType specified.");
        }

        private static Couchbase.KeyValue.RangeScan.ScanTerm? ConvertScanTerm(ScanTermChoice st)
        {
            if (st.ChoiceCase == ScanTermChoice.ChoiceOneofCase.Default)
            {
                return null;
            }

            if (st.ChoiceCase == ScanTermChoice.ChoiceOneofCase.Maximum)
            {
                return null;
            }

            if (st.ChoiceCase == ScanTermChoice.ChoiceOneofCase.Minimum)
            {
                return null;
            }

            if (st.ChoiceCase == ScanTermChoice.ChoiceOneofCase.Term)
            {
                var stt = st.Term;
                if (stt.TermCase == Couchbase.Grpc.Protocol.Sdk.Kv.RangeScan.ScanTerm.TermOneofCase.AsBytes)
                {
                    throw new UnsupportedException("ScanTerms cannot be constructed from Byte arrays anymore.");
                }
                else if (stt.TermCase == Couchbase.Grpc.Protocol.Sdk.Kv.RangeScan.ScanTerm.TermOneofCase.AsString)
                {
                    if (stt.HasExclusive && stt.Exclusive)
                    {
                        return KeyValue.RangeScan.ScanTerm.Exclusive(stt.AsString);
                    }
                    else
                    {
                        return KeyValue.RangeScan.ScanTerm.Inclusive(stt.AsString);
                    }
                }
            }

            throw new UnsupportedException("Unknown range specified.");
        }

        public static Couchbase.KeyValue.RangeScan.ScanOptions? CreateOptions(Couchbase.Grpc.Protocol.Sdk.Kv.RangeScan.Scan request)
        {
            if (request.Options != null)
            {

                var opts = request.Options;
                var ret = new Couchbase.KeyValue.RangeScan.ScanOptions();
                if (opts.HasTimeoutMsecs) ret.Timeout(TimeSpan.FromMilliseconds(opts.TimeoutMsecs));
                if (opts.IdsOnly) ret.IdsOnly(opts.IdsOnly);
                if (opts.ConsistentWith != null) ret.ConsistentWith(ConvertMutationState(opts.ConsistentWith));
                if (opts.Transcoder != null) ret.Transcoder(ConvertTranscoder(opts.Transcoder));
                if (opts.HasBatchByteLimit) ret.ByteLimit((uint)opts.BatchByteLimit);
                if (opts.HasBatchItemLimit) ret.ByteLimit((uint)opts.BatchItemLimit);
                if (opts.HasBatchTimeLimit) ret.TimeLimit((uint)opts.BatchTimeLimit);
                //ParentSpan?

                return ret;
            }

            return null;
        }

        #endregion
        #region Collection Management

        public static Couchbase.Management.Collections.CreateCollectionOptions ConvertOptions(
            Couchbase.Grpc.Protocol.Sdk.Bucket.CollectionManager.CreateCollectionOptions? protoOptions)
        {
            var options = new Couchbase.Management.Collections.CreateCollectionOptions();
            if (protoOptions != null)
            {
                if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
                // if (protoOptions.HasParentSpanId); No Tracing in SDK Options
            }

            return options;
        }

        public static Couchbase.Management.Collections.CreateScopeOptions ConvertOptions(
            Couchbase.Grpc.Protocol.Sdk.Bucket.CollectionManager.CreateScopeOptions? protoOptions)
        {
            var options = new Couchbase.Management.Collections.CreateScopeOptions();
            if (protoOptions != null)
            {
                if (protoOptions.HasTimeoutMsecs) options.CancellationToken(new CancellationTokenSource(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs)).Token);
                // if (protoOptions.HasParentSpanId); No Tracing in SDK Options
            }
            return options;
        }

        public static Couchbase.Management.Collections.DropScopeOptions ConvertOptions(
            Couchbase.Grpc.Protocol.Sdk.Bucket.CollectionManager.DropScopeOptions? protoOptions)
        {
            var options = new Couchbase.Management.Collections.DropScopeOptions();
            if (protoOptions != null)
            {
                if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
                // if (protoOptions.HasParentSpanId); No Tracing in SDK Options
            }
            return options;
        }

        public static Couchbase.Management.Buckets.DropCollectionOptions ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Bucket.CollectionManager.DropCollectionOptions? protoOptions)
        {
            var options = new Couchbase.Management.Buckets.DropCollectionOptions();
            if (protoOptions != null)
            {
                if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
                // if (protoOptions.HasParentSpanId); No Tracing in SDK Options
            }
            return options;
        }

        public static Couchbase.Management.Collections.UpdateCollectionOptions ConvertOptions(
            Couchbase.Grpc.Protocol.Sdk.Bucket.CollectionManager.UpdateCollectionOptions? protoOptions)
        {
            var options = Couchbase.Management.Collections.UpdateCollectionOptions.Default;
            if (protoOptions != null)
            {
                if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
                // if (protoOptions.HasParentSpanId); No Tracing in SDK Options
            }
            return options;
        }

        public static GetAllScopesOptions ConvertOptions(
            Couchbase.Grpc.Protocol.Sdk.Bucket.CollectionManager.GetAllScopesOptions? protoOptions)
        {
            var options = new GetAllScopesOptions();
            if (protoOptions != null)
            {
                if (protoOptions.HasTimeoutMsecs)
                    options.CancellationToken(
                        new CancellationTokenSource(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs)).Token);
                // if (protoOptions.HasParentSpanId); No Tracing in SDK Options
            }
            return options;
        }

        public static CreateCollectionSettings ConvertSettings(Couchbase.Grpc.Protocol.Sdk.Bucket.CollectionManager.CreateCollectionSettings? protoSettings)
        {
            bool? history = null;
            TimeSpan? expiry = null;
            if (protoSettings != null)
            {
                if (protoSettings.HasHistory) history = protoSettings.History;
                if (protoSettings.HasExpirySecs) expiry = TimeSpan.FromSeconds(protoSettings.ExpirySecs);
            }
            return new CreateCollectionSettings(expiry, history);

        }

        public static UpdateCollectionSettings ConvertSettings(Couchbase.Grpc.Protocol.Sdk.Bucket.CollectionManager.UpdateCollectionSettings? protoSettings)
        {
            bool? history = null;
            TimeSpan? expiry = null;
            if (protoSettings != null)
            {
                if (protoSettings.HasHistory) history = protoSettings.History;
                if (protoSettings.HasExpirySecs) expiry = TimeSpan.FromSeconds(protoSettings.ExpirySecs);
            }
            return new UpdateCollectionSettings(expiry, history);
        }

        #endregion

        #region BucketManager

        public static BucketSettings ConvertSettings(
            Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.BucketSettings? protoSettings)
        {
            var settings = new BucketSettings();
            if (protoSettings != null)
            {
                if (protoSettings.HasHistoryRetentionCollectionDefault) settings.HistoryRetentionCollectionDefault = protoSettings.HistoryRetentionCollectionDefault;
                if (protoSettings.HasHistoryRetentionSeconds) settings.HistoryRetentionDuration = TimeSpan.FromSeconds(protoSettings.HistoryRetentionSeconds);
                if (protoSettings.HasHistoryRetentionBytes) settings.HistoryRetentionBytes = protoSettings.HistoryRetentionBytes;
                if (protoSettings.HasNumVbuckets) settings.NumVBuckets = protoSettings.NumVbuckets;
                settings.Name = protoSettings.Name;
                // RamQuotaMB is non-optional in the proto so we cannot use a Has* check.
                // Treat 0 as "user did not set it" to avoid marking the property as explicitly set,
                // which is required for the SDK contract that only user-specified parameters are sent.
                if (protoSettings.RamQuotaMB > 0) settings.RamQuotaMB = protoSettings.RamQuotaMB;
                if (protoSettings.HasFlushEnabled) settings.FlushEnabled = protoSettings.FlushEnabled;
                if (protoSettings.HasNumReplicas) settings.NumReplicas = protoSettings.NumReplicas;
                if (protoSettings.HasReplicaIndexes) settings.ReplicaIndexes = protoSettings.ReplicaIndexes;
                if (protoSettings.HasBucketType) settings.BucketType = ConvertBucketType(protoSettings.BucketType);
                if (protoSettings.HasEvictionPolicy) settings.EvictionPolicy = ConvertEvictionPolicy(protoSettings.EvictionPolicy);
                if (protoSettings.HasMaxExpirySeconds) settings.MaxTtl = protoSettings.MaxExpirySeconds;
                if (protoSettings.HasCompressionMode) settings.CompressionMode = ConvertCompressionMode(protoSettings.CompressionMode);
                if (protoSettings.HasMinimumDurabilityLevel) settings.DurabilityMinimumLevel = ConvertDurabilityLevel(protoSettings.MinimumDurabilityLevel);
                if (protoSettings.HasStorageBackend) settings.StorageBackend = ConvertStorageBackend(protoSettings.StorageBackend);
            }

            return settings;
        }


        /// <summary>
        /// Verbose method to add init-only parameters to the BucketSettings object.
        /// These were changed in 3.7.1 to have a setter.
        /// </summary>
        /// <param name="protoSettings">The protobuf BucketSettings</param>
        /// <returns>A new BucketSettings object</returns>
        private static BucketSettings CreateBucketSettingsWithInitOnlyParameters(
            Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.BucketSettings protoSettings)
        {
            var truple = (protoSettings.HasHistoryRetentionCollectionDefault, protoSettings.HasHistoryRetentionSeconds, protoSettings.HasHistoryRetentionBytes);

            return (truple) switch
            {
                (false, false, false) => new BucketSettings(),
                (false, false, true) => new BucketSettings
                    { HistoryRetentionBytes = protoSettings.HistoryRetentionBytes },
                (false, true, true) => new BucketSettings
                {
                    HistoryRetentionDuration = TimeSpan.FromSeconds(protoSettings.HistoryRetentionSeconds),
                    HistoryRetentionBytes = protoSettings.HistoryRetentionBytes
                },
                (true, true, true) => new BucketSettings
                {
                    HistoryRetentionCollectionDefault = protoSettings.HistoryRetentionCollectionDefault,
                    HistoryRetentionDuration = TimeSpan.FromSeconds(protoSettings.HistoryRetentionSeconds),
                    HistoryRetentionBytes = protoSettings.HistoryRetentionBytes
                },
                (true, true, false) => new BucketSettings
                {
                    HistoryRetentionCollectionDefault = protoSettings.HistoryRetentionCollectionDefault,
                    HistoryRetentionDuration = TimeSpan.FromSeconds(protoSettings.HistoryRetentionSeconds)
                },
                (true, false, false) => new BucketSettings
                    { HistoryRetentionCollectionDefault = protoSettings.HistoryRetentionCollectionDefault },
                (true, false, true) => new BucketSettings
                {
                    HistoryRetentionCollectionDefault = protoSettings.HistoryRetentionCollectionDefault,
                    HistoryRetentionBytes = protoSettings.HistoryRetentionBytes
                },
                (false, true, false) => new BucketSettings
                    { HistoryRetentionDuration = TimeSpan.FromSeconds(protoSettings.HistoryRetentionSeconds) }
            };
        }


        private static EvictionPolicyType ConvertEvictionPolicy(Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.EvictionPolicyType evictionPolicy)
        {
            return evictionPolicy switch
            {
                Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.EvictionPolicyType.Full => EvictionPolicyType.FullEviction,
                Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.EvictionPolicyType.ValueOnly => EvictionPolicyType.ValueOnly,
                Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.EvictionPolicyType.NoEviction => EvictionPolicyType.NoEviction,
                Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.EvictionPolicyType.NotRecentlyUsed => EvictionPolicyType.NotRecentlyUsed,
                _ => throw new NotSupportedException("Unknown eviction policy")
            };
        }

        private static BucketType ConvertBucketType(Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.BucketType bucketType)
        {
            return bucketType switch
            {
                Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.BucketType.Couchbase => BucketType.Couchbase,
                Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.BucketType.Memcached => BucketType.Memcached,
                Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.BucketType.Ephemeral => BucketType.Ephemeral,
                _ => throw new NotSupportedException("Unknown bucket type")
            };
        }

        private static CompressionMode ConvertCompressionMode(Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.CompressionMode compressionMode)
        {
            return compressionMode switch
            {
                Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.CompressionMode.Off => CompressionMode.Off,
                Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.CompressionMode.Passive => CompressionMode.Passive,
                Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.CompressionMode.Active => CompressionMode.Active,
                _ => throw new NotSupportedException("Unknown compression mode")
            };
        }

        private static StorageBackend ConvertStorageBackend(Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.StorageBackend storageBackend)
        {
            return storageBackend switch
            {
                Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.StorageBackend.Couchstore => StorageBackend.Couchstore,
                Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.StorageBackend.Magma => StorageBackend.Magma,
                _ => throw new NotSupportedException("Unknown storage backend")
            };
        }

        public static Management.Buckets.GetBucketOptions ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.GetBucketOptions? protoOptions)
        {
            var options = new Management.Buckets.GetBucketOptions();
            if (protoOptions != null)
            {
                if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
                // Note: Parent span not supported in SDK options
            }
            return options;
        }

        public static Management.Buckets.GetAllBucketsOptions ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.GetAllBucketsOptions? protoOptions)
        {
            var options = new Management.Buckets.GetAllBucketsOptions();
            if (protoOptions != null)
            {
                if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
                // Note: Parent span not supported in SDK options
            }
            return options;
        }

        public static Management.Buckets.CreateBucketOptions ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.CreateBucketOptions? protoOptions)
        {
            var options = new Management.Buckets.CreateBucketOptions();
            if (protoOptions != null)
            {
                if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
                // Note: Parent span not supported in SDK options
            }
            return options;
        }

        public static Management.Buckets.DropBucketOptions ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.DropBucketOptions? protoOptions)
        {
            var options = new Management.Buckets.DropBucketOptions();
            if (protoOptions != null)
            {
                if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
                // Note: Parent span not supported in SDK options
            }
            return options;
        }

        public static Management.Buckets.FlushBucketOptions ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.FlushBucketOptions? protoOptions)
        {
            var options = new Management.Buckets.FlushBucketOptions();
            if (protoOptions != null)
            {
                if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
                // Note: Parent span not supported in SDK options
            }
            return options;
        }

        public static Management.Buckets.UpdateBucketOptions ConvertOptions(Couchbase.Grpc.Protocol.Sdk.Cluster.BucketManager.UpdateBucketOptions? protoOptions)
        {
            var options = new Management.Buckets.UpdateBucketOptions();
            if (protoOptions != null)
            {
                if (protoOptions.HasTimeoutMsecs) options.Timeout(TimeSpan.FromMilliseconds(protoOptions.TimeoutMsecs));
                // Note: Parent span not supported in SDK options
            }
            return options;
        }
        #endregion
    }