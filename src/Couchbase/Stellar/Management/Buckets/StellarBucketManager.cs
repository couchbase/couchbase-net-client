#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Retry;
using Couchbase.Management.Buckets;
using Couchbase.Protostellar.Admin.Bucket.V1;
using Couchbase.Protostellar.KV.V1;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Core.Retry;
using Couchbase.Stellar.Util;
using Couchbase.Utils;
using BucketType = Couchbase.Protostellar.Admin.Bucket.V1.BucketType;
using CompressionMode = Couchbase.Protostellar.Admin.Bucket.V1.CompressionMode;
using ConflictResolutionType = Couchbase.Protostellar.Admin.Bucket.V1.ConflictResolutionType;
using StorageBackend = Couchbase.Protostellar.Admin.Bucket.V1.StorageBackend;

namespace Couchbase.Stellar.Management.Buckets;

#nullable enable

internal class StellarBucketManager : IBucketManager
{
    private readonly BucketAdminService.BucketAdminServiceClient _bucketAdminClient;
    private readonly StellarCluster _stellarCluster;
    private readonly IRetryOrchestrator _retryHandler;
    public StellarBucketManager(StellarCluster stellarCluster)
    {
        _stellarCluster = stellarCluster;
        _bucketAdminClient = new BucketAdminService.BucketAdminServiceClient(stellarCluster.GrpcChannel);
        _retryHandler = stellarCluster.RetryHandler;
    }

    public async Task CreateBucketAsync(BucketSettings settings, CreateBucketOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? CreateBucketOptions.DefaultReadOnly;
        var createBucketRequest = new CreateBucketRequest
        {
            BucketName = settings.Name
        };

        if (settings.IsSet(nameof(BucketSettings.BucketType))) createBucketRequest.BucketType = settings.BucketType.ToProto();
        if (settings.IsSet(nameof(BucketSettings.RamQuotaMB))) createBucketRequest.RamQuotaMb = (ulong)settings.RamQuotaMB;
        if (settings.IsSet(nameof(BucketSettings.NumReplicas))) createBucketRequest.NumReplicas = (uint)settings.NumReplicas;
        if (settings.IsSet(nameof(BucketSettings.FlushEnabled))) createBucketRequest.FlushEnabled = settings.FlushEnabled;
        if (settings.IsSet(nameof(BucketSettings.ReplicaIndexes))) createBucketRequest.ReplicaIndexes = settings.ReplicaIndexes;
        if (settings.IsSet(nameof(BucketSettings.MaxTtl))) createBucketRequest.MaxExpirySecs = (uint)settings.MaxTtl;
        if (settings.EvictionPolicy.HasValue) createBucketRequest.EvictionMode = settings.EvictionPolicy.Value.ToProto();
        if (settings.CompressionMode.HasValue) createBucketRequest.CompressionMode = settings.CompressionMode.Value.ToProto();
        if (settings.StorageBackend.HasValue) createBucketRequest.StorageBackend = settings.StorageBackend.Value.ToProto();
        if (settings.ConflictResolutionType.HasValue) createBucketRequest.ConflictResolutionType = settings.ConflictResolutionType.Value.ToProto();
        if (settings.IsSet(nameof(BucketSettings.DurabilityMinimumLevel)) && settings.DurabilityMinimumLevel.TryConvertToProto(out var protoDurability)) createBucketRequest.MinimumDurabilityLevel = protoDurability;

        using var span = _stellarCluster.ManagerSpan(OuterRequestSpans.ManagerSpan.Bucket.CreateBucket, OuterRequestSpans.ServiceSpan.Management, "CreateBucket", options?.RequestSpanValue);
        var stellarRequest = new StellarRequest
        {
            Span = span,
            Idempotent = false,
            Token = opts.CancellationToken,
            Timeout = _stellarCluster.ClusterOptions.ManagementTimeout
        };
        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
        return;

        async Task<CreateBucketResponse> GrpcCall()
        {
            return await _bucketAdminClient.CreateBucketAsync(createBucketRequest, _stellarCluster.GrpcCallOptions(stellarRequest.RemainingTimeout, opts.CancellationToken)).ConfigureAwait(false);
        }
    }

    public async Task UpdateBucketAsync(BucketSettings settings, UpdateBucketOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? UpdateBucketOptions.DefaultReadOnly;
        var updateBucketRequest = new UpdateBucketRequest
        {
            BucketName = settings.Name
        };

        if (settings.IsSet(nameof(BucketSettings.RamQuotaMB))) updateBucketRequest.RamQuotaMb = (ulong)settings.RamQuotaMB;
        if (settings.IsSet(nameof(BucketSettings.NumReplicas))) updateBucketRequest.NumReplicas = (uint)settings.NumReplicas;
        if (settings.IsSet(nameof(BucketSettings.FlushEnabled))) updateBucketRequest.FlushEnabled = settings.FlushEnabled;
        if (settings.IsSet(nameof(BucketSettings.MaxTtl))) updateBucketRequest.MaxExpirySecs = (uint)settings.MaxTtl;
        if (settings.EvictionPolicy.HasValue) updateBucketRequest.EvictionMode = settings.EvictionPolicy.Value.ToProto();
        if (settings.CompressionMode.HasValue) updateBucketRequest.CompressionMode = settings.CompressionMode.Value.ToProto();
        if (settings.IsSet(nameof(BucketSettings.DurabilityMinimumLevel)) && settings.DurabilityMinimumLevel.TryConvertToProto(out var protoDurability)) updateBucketRequest.MinimumDurabilityLevel = protoDurability;

        using var span = _stellarCluster.ManagerSpan(OuterRequestSpans.ManagerSpan.Bucket.UpdateBucket, OuterRequestSpans.ServiceSpan.Management, "UpdateBucket", options?.RequestSpanValue);
        var stellarRequest = new StellarRequest
        {
            Span = span,
            Idempotent = false,
            Token = opts.CancellationToken,
            Timeout = _stellarCluster.ClusterOptions.ManagementTimeout
        };
        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
        return;

        async Task<UpdateBucketResponse> GrpcCall()
        {
            return await _bucketAdminClient.UpdateBucketAsync(updateBucketRequest, _stellarCluster.GrpcCallOptions(stellarRequest.RemainingTimeout, opts.CancellationToken)).ConfigureAwait(false);
        }
    }

    public async Task DropBucketAsync(string bucketName, DropBucketOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? DropBucketOptions.DefaultReadOnly;
        var dropBucketRequest = new DeleteBucketRequest
        {
            BucketName = bucketName
        };

        using var span = _stellarCluster.ManagerSpan(OuterRequestSpans.ManagerSpan.Bucket.DropBucket, OuterRequestSpans.ServiceSpan.Management, "DeleteBucket", options?.RequestSpanValue);
        var stellarRequest = new StellarRequest
        {
            Span = span,
            Idempotent = false,
            Token = opts.CancellationToken,
            Timeout = _stellarCluster.ClusterOptions.ManagementTimeout
        };
        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
        return;

        async Task<DeleteBucketResponse> GrpcCall()
        {
            return await _bucketAdminClient.DeleteBucketAsync(dropBucketRequest, _stellarCluster.GrpcCallOptions(stellarRequest.RemainingTimeout, opts.CancellationToken)).ConfigureAwait(false);
        }
    }

    public async Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(GetAllBucketsOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetAllBucketsOptions.DefaultReadOnly;
        using var span = _stellarCluster.ManagerSpan(OuterRequestSpans.ManagerSpan.Bucket.GetAllBuckets,
            OuterRequestSpans.ServiceSpan.Management, "ListBuckets", options?.RequestSpanValue);
        return await ListBucketsAsync(span, opts.CancellationToken).ConfigureAwait(false);
    }

    public async Task<BucketSettings> GetBucketAsync(string bucketName, GetBucketOptions? options = null)
    {
        // RFC 77: GetBucket is a ListBuckets call, so it has one span with db.operation ListBuckets.
        using var span = _stellarCluster.ManagerSpan(OuterRequestSpans.ManagerSpan.Bucket.GetBucket,
            OuterRequestSpans.ServiceSpan.Management, "ListBuckets", options?.RequestSpanValue);
        var allBuckets = await ListBucketsAsync(span, options?.TokenValue ?? default).ConfigureAwait(false);
        try
        {
            return allBuckets[bucketName];
        }
        catch (KeyNotFoundException)
        {
            // ListBuckets succeeded, so the span status is Ok. As in classic mode, a missing bucket is an error.
            span.SetStatus(RequestSpanStatusCode.Error);
            throw new BucketNotFoundException($"Bucket '{_stellarCluster.Redactor.MetaDataString(bucketName)}' not found.");
        }
    }

    private async Task<Dictionary<string, BucketSettings>> ListBucketsAsync(IRequestSpan span, CancellationToken token)
    {
        var listBucketsRequest = new ListBucketsRequest();

        var stellarRequest = new StellarRequest
        {
            Span = span,
            Idempotent = true,
            Token = token,
            Timeout = _stellarCluster.ClusterOptions.ManagementTimeout
        };
        var response = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);

        var buckets = response.Buckets.ToDictionary(bucket => bucket.BucketName, bucket => new BucketSettings
        {
            Name = bucket.BucketName,
            BucketType = bucket.BucketType.ToCore(),
            RamQuotaMB = (long)bucket.RamQuotaMb,
            FlushEnabled = bucket.FlushEnabled,
            NumReplicas = (int)bucket.NumReplicas,
            ReplicaIndexes = bucket.ReplicaIndexes,
            ConflictResolutionType = bucket.ConflictResolutionType.ToCore(),
            EvictionPolicy = bucket.EvictionMode.ToCore(),
            MaxTtl = (int)bucket.MaxExpirySecs,
            CompressionMode = bucket.CompressionMode.ToCore(),
            DurabilityMinimumLevel = bucket.MinimumDurabilityLevel.ToCore(),
            StorageBackend = bucket.StorageBackend.ToCore()
        });
        return buckets;

        async Task<ListBucketsResponse> GrpcCall()
        {
            return await _bucketAdminClient.ListBucketsAsync(listBucketsRequest, _stellarCluster.GrpcCallOptions(stellarRequest.RemainingTimeout, token)).ConfigureAwait(false);
        }
    }

    public Task FlushBucketAsync(string bucketName, FlushBucketOptions? options = null)=>
        throw ThrowHelper.ThrowFeatureNotAvailableException(nameof(FlushBucketAsync), "Protostellar");
}
#endif
