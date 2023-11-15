using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Couchbase.Management.Query;
using Couchbase.Protostellar.KV.V1;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Management.Query;
using Couchbase.Stellar.Util;
using Couchbase.Utils;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ExistsRequest = Couchbase.Protostellar.KV.V1.ExistsRequest;
using GetAndLockRequest = Couchbase.Protostellar.KV.V1.GetAndLockRequest;
using GetAndTouchRequest = Couchbase.Protostellar.KV.V1.GetAndTouchRequest;
using GetRequest = Couchbase.Protostellar.KV.V1.GetRequest;
using InsertRequest = Couchbase.Protostellar.KV.V1.InsertRequest;
using LookupInRequest = Couchbase.Protostellar.KV.V1.LookupInRequest;
using MutateInRequest = Couchbase.Protostellar.KV.V1.MutateInRequest;
using RemoveRequest = Couchbase.Protostellar.KV.V1.RemoveRequest;
using ReplaceRequest = Couchbase.Protostellar.KV.V1.ReplaceRequest;
using TouchRequest = Couchbase.Protostellar.KV.V1.TouchRequest;
using UnlockRequest = Couchbase.Protostellar.KV.V1.UnlockRequest;
using UpsertRequest = Couchbase.Protostellar.KV.V1.UpsertRequest;

namespace Couchbase.Stellar.KeyValue;

internal class StellarCollection : ICouchbaseCollection
{
    private readonly StellarCollectionQueryIndexManager _stellarCollectionQueryIndexes;
    private readonly StellarScope _stellarScope;
    private readonly StellarCluster _stellarCluster;
    private readonly KvService.KvServiceClient _kvClient;
    private readonly string _scopeName;
    private readonly string _bucketName;

    public const string DefaultCollectionName = "_default";

    public StellarCollection(string collectionName, StellarScope stellarScope, StellarCluster stellarCluster)
    {
        Name = collectionName;
        IsDefaultCollection = Name == DefaultCollectionName;
        _stellarScope = stellarScope;
        _stellarCluster = stellarCluster;
        _kvClient = new KvService.KvServiceClient(_stellarCluster.GrpcChannel);
        _bucketName = _stellarScope.Bucket.Name;
        _scopeName = _stellarScope.Name;
        _stellarCollectionQueryIndexes = new StellarCollectionQueryIndexManager(stellarCluster.QueryIndexes, _bucketName, _scopeName, Name);
    }

    public uint? Cid => throw new UnsupportedInProtostellarException("Cid (Collection ID)");

    public string Name { get; }

    public IScope Scope => _stellarScope;

    public IBinaryCollection Binary => throw new UnsupportedInProtostellarException("Binary Operations");

    public bool IsDefaultCollection { get; }

    public ICollectionQueryIndexManager QueryIndexes => _stellarCollectionQueryIndexes;

    public async Task<IExistsResult> ExistsAsync(string id, ExistsOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? ExistsOptions.DefaultReadOnly;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.GetMetaExists, opts.RequestSpan);
        var request = KeyedRequest<ExistsRequest>(id);

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        var response = await _kvClient.ExistsAsync(request, callOptions).ConfigureAwait(false);
        return new ExistsResult(Exists: response.Result, Cas: response.Cas);
    }

    public IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(string id, GetAllReplicasOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetAllReplicasOptions.DefaultReadOnly;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.GetAllReplicas, opts.RequestSpan);
        var request = KeyedRequest<GetAllReplicasRequest>(id);

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Token);
        var response = _kvClient.GetAllReplicas(request, callOptions);
        var serializer = opts.Transcoder?.Serializer ?? _stellarCluster.TypeSerializer;

        IEnumerable<Task<IGetReplicaResult>> results = response.ResponseStream
            .ReadAllAsync()
            .Select(replicaResult => Task.FromResult(replicaResult.AsGetReplicaResult(serializer))).ToEnumerable();

        return results;
    }

    public async Task<IGetResult> GetAndLockAsync(string id, TimeSpan expiry, GetAndLockOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetAndLockOptions.DefaultReadOnly;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.GetAndLock, opts.RequestSpan);
        var serializer = opts.Transcoder?.Serializer ?? _stellarCluster.TypeSerializer;

        var request = KeyedRequest<GetAndLockRequest>(id);
        request.LockTime = expiry.ToTtl();

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        var response = await _kvClient.GetAndLockAsync(request, callOptions).ConfigureAwait(false);
        return response.AsGetResult(serializer);
    }

    public async Task<IGetResult> GetAndTouchAsync(string id, TimeSpan expiry, GetAndTouchOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetAndTouchOptions.DefaultReadOnly;
        var serializer = opts.Transcoder?.Serializer ?? _stellarCluster.TypeSerializer;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.GetAndTouch, opts.RequestSpan);

        var request = KeyedRequest<GetAndTouchRequest>(id);
        request.ExpiryTime = Timestamp.FromDateTime(expiry.FromNow());

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        var response = await _kvClient.GetAndTouchAsync(request, callOptions).ConfigureAwait(false);
        return response.AsGetResult(serializer);
    }

    public Task<IGetReplicaResult> GetAnyReplicaAsync(string id, GetAnyReplicaOptions? options = null)
    {
        throw new UnsupportedInProtostellarException(nameof(GetAnyReplicaAsync));
    }

    public async Task<IGetResult> GetAsync(string id, GetOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetOptions.DefaultReadOnly;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.Get, opts.RequestSpan);
        var serializer = opts.Transcoder?.Serializer ?? _stellarCluster.TypeSerializer;

        var request = KeyedRequest<GetRequest>(id);

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        var response = await _kvClient.GetAsync(request, callOptions).ConfigureAwait(false);
        return response.AsGetResult(serializer);
    }

    public async Task<IMutationResult> InsertAsync<T>(string id, T content, InsertOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? InsertOptions.DefaultReadOnly;
        var serializer = opts.Transcoder?.Serializer ?? _stellarCluster.TypeSerializer;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.AddInsert, opts.RequestSpan);

        var request = await ContentRequest<InsertRequest, T>(
            key: id,
            contentFlags: 0, // FIXME:  This probably needs to be left off ContentRequest and used to set up the serializer instead.
            content: content,
            preserveTtl: false,
            expiry: opts.Expiry,
            kvDurabilityLevel: opts.DurabilityLevel,
            serializer: serializer,
            cancellationToken: opts.Token).ConfigureAwait(false);

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        var response = await _kvClient.InsertAsync(request, callOptions).ConfigureAwait(false);
        return new MutationResult(response.Cas, Expiry: null)
        {
            MutationToken = response.MutationToken
        };
    }

    public async Task<ILookupInResult> LookupInAsync(string id, IEnumerable<LookupInSpec> specs, LookupInOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? LookupInOptions.DefaultReadOnly;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.LookupIn, opts.RequestSpan);

        var request = KeyedRequest<LookupInRequest>(id);
        foreach (var spec in specs)
        {
            request.Specs.Add(new LookupInRequest.Types.Spec()
            {
                Path = spec.Path,
                Operation = spec.OpCode.ToProtoLookupInCode(),
                Flags = spec.PathFlags.ToProtoLookupInFlags(),
            });
        }

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        var response = await _kvClient.LookupInAsync(request, callOptions).ConfigureAwait(false);
        return new LookupInResult(response, request, _stellarCluster.TypeSerializer);
    }

    public Task<ILookupInReplicaResult> LookupInAnyReplicaAsync(string id, IEnumerable<LookupInSpec> specs, LookupInAnyReplicaOptions? options = null)
    {
        throw new UnsupportedInProtostellarException(nameof(LookupInAnyReplicaAsync));
    }

    public IAsyncEnumerable<ILookupInReplicaResult> LookupInAllReplicasAsync(string id, IEnumerable<LookupInSpec> specs, LookupInAllReplicasOptions? options = null)
    {
        throw new UnsupportedInProtostellarException(nameof(LookupInAllReplicasAsync));
    }

    public async Task<IMutateInResult> MutateInAsync(string id, IEnumerable<MutateInSpec> specs, MutateInOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? MutateInOptions.DefaultReadOnly;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.LookupIn, opts.RequestSpan);

        var request = KeyedRequest<MutateInRequest>(id);
        request.Cas = opts.Cas;
        var (expirySecs, expiryTimestamp) = CalculateExpiry(opts.Expiry, opts.PreserveTtl);
        if (expirySecs.HasValue)
        {
            request.ExpirySecs = expirySecs.Value;
        }
        else if (expiryTimestamp != null)
        {
            request.ExpiryTime = expiryTimestamp;
        }
        var durability = opts.DurabilityLevel.ToProto();
        if (durability.HasValue)
        {
            request.DurabilityLevel = durability.Value;
        }

        request.StoreSemantic = opts.StoreSemantics.ToProto();

        foreach (var spec in specs)
        {
            var newSpec = new MutateInRequest.Types.Spec()
            {
                Path = spec.Path,
                Operation = spec.OpCode.ToProtoMutateInCode(),
                Flags = spec.PathFlags.ToProtoMutateInFlags(),
            };

            if (spec.Value is not null)
            {
                newSpec.Content = await SerializeToByteString(spec.Value, _stellarCluster.TypeSerializer, opts.Token).ConfigureAwait(false);
            }

            request.Specs.Add(newSpec);
        }

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        var response = await _kvClient.MutateInAsync(request, callOptions).ConfigureAwait(false);
        return new MutateInResult(response, request, _stellarCluster.TypeSerializer);
    }

    public async Task RemoveAsync(string id, RemoveOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? RemoveOptions.DefaultReadOnly;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.DeleteRemove, opts.RequestSpan);

        var request = KeyedRequest<RemoveRequest>(id);
        request.Cas = opts.Cas;

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        _ = await _kvClient.RemoveAsync(request, callOptions);
    }

    public async Task<IMutationResult> ReplaceAsync<T>(string id, T content, ReplaceOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? ReplaceOptions.DefaultReadOnly;
        var serializer = opts.Transcoder?.Serializer ?? _stellarCluster.TypeSerializer;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.Replace, opts.RequestSpan);
        var request = await ContentRequest<ReplaceRequest, T>(
            key: id,
            contentFlags: default, // FIXME: handle content flags
            content: content,
            preserveTtl: opts.PreserveTtl,
            expiry: opts.Expiry,
            kvDurabilityLevel: opts.DurabilityLevel,
            serializer: serializer,
            cancellationToken: opts.Token).ConfigureAwait(false);

        request.Cas = opts.Cas;

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);;
        var response = await _kvClient.ReplaceAsync(request, callOptions);
        return new MutationResult(response.Cas, null)
        {
            MutationToken = response.MutationToken
        };

    }

    public IAsyncEnumerable<IScanResult> ScanAsync(IScanType scanType, ScanOptions? options = null)
    {
        throw new UnsupportedInProtostellarException(nameof(ScanAsync));
    }

    public async Task TouchAsync(string id, TimeSpan expiry, TouchOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? TouchOptions.DefaultReadOnly;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.Touch, opts.RequestSpan);

        var request = KeyedRequest<TouchRequest>(id);
        request.ExpirySecs = (uint)expiry.TotalSeconds;

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        _ = await _kvClient.TouchAsync(request, callOptions);
    }

    public Task UnlockAsync<T>(string id, ulong cas, UnlockOptions? options = null) =>
        UnlockAsync(id, cas, options);

    public async Task UnlockAsync(string id, ulong cas, UnlockOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? UnlockOptions.DefaultReadOnly;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.Unlock, opts.RequestSpan);

        var request = KeyedRequest<UnlockRequest>(id);
        request.Cas = cas;

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        _ = await _kvClient.UnlockAsync(request, callOptions);
    }

    public async Task<IMutationResult> UpsertAsync<T>(string id, T content, UpsertOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? UpsertOptions.DefaultReadOnly;
        var serializer = opts.Transcoder?.Serializer ?? _stellarCluster.TypeSerializer;
        using var childSpan = TraceSpan(OuterRequestSpans.ServiceSpan.Kv.SetUpsert, opts.RequestSpan);

        var request = await ContentRequest<UpsertRequest, T>(
            key: id,
            contentFlags: default, // FIXME: handle content flags
            content: content,
            preserveTtl: opts.PreserveTtl,
            expiry: opts.Expiry,
            kvDurabilityLevel: opts.DurabilityLevel,
            serializer: serializer,
            cancellationToken: opts.Token).ConfigureAwait(false);

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        var upsertResponse = await _kvClient.UpsertAsync(request, callOptions);
        return new MutationResult(upsertResponse.Cas, null)
        {
            MutationToken = upsertResponse.MutationToken
        };
    }


    private static async Task<ByteString> SerializeToByteString<T>(T content, ITypeSerializer serializer,
        CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await serializer.SerializeAsync(ms, content, cancellationToken).ConfigureAwait(false);
        ms.Position = 0;
        var serializedContent =
            await ByteString.FromStreamAsync(ms, cancellationToken: cancellationToken).ConfigureAwait(false);
        return serializedContent;
    }

    private T KeyedRequest<T>(string key) where T : IKeySpec, new()
    {
        var request = new T();
        request.BucketName = _bucketName;
        request.ScopeName = _scopeName;
        request.CollectionName = this.Name;
        request.Key = key;
        return request;
    }

    private (uint? expirySecs, Timestamp? grpcExpiry) CalculateExpiry(TimeSpan expiry, bool preserveTtl)
    {
        if (preserveTtl)
        {
            // following behavior defined in Go and implied by protostellar
            if (expiry != default)
            {
                throw new NotSupportedException("Cannot mix Expiry and PreserveTtl = true");
            }


#pragma warning disable CS8625
            // TODO: default value not friendly with proto-defined types.
            return (0, null);
#pragma warning restore CS8625
        }
        else if (expiry != default)
        {
            return (null, Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.Add(expiry)));
        }

        return (null, null);
    }

    private async Task<TRequest> ContentRequest<TRequest, TContent>(
        string key,
        uint contentFlags,
        TContent content,
        bool preserveTtl,
        TimeSpan expiry,
        Couchbase.KeyValue.DurabilityLevel kvDurabilityLevel,
        ITypeSerializer serializer,
        CancellationToken cancellationToken)
        where TRequest : IKeySpec, IContentRequest, new()
    {
        var request = KeyedRequest<TRequest>(key);
        request.ContentFlags = contentFlags;
        var (expirySecs, expiryTimestamp) = CalculateExpiry(expiry, preserveTtl);
        if (expirySecs.HasValue)
        {
            request.ExpirySecs = expirySecs.Value;
        }
        else if (expiryTimestamp != null)
        {
            request.ExpiryTime = expiryTimestamp;
        }

        var durabilityLevel = kvDurabilityLevel.ToProto();
        if (durabilityLevel.HasValue)
        {
            request.DurabilityLevel = durabilityLevel.Value;
        }

        request.Content = await SerializeToByteString(content, serializer, cancellationToken).ConfigureAwait(false);
        return request;
    }

    private IRequestSpan TraceSpan(string attr, IRequestSpan? parentSpan) =>
        _stellarCluster.RequestTracer.RequestSpan(attr, parentSpan);
}
