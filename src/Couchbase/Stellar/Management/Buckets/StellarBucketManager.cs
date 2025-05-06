#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            BucketName = settings.Name,
            BucketType = settings.BucketType.ToProto(),
            RamQuotaMb = (ulong)settings.RamQuotaMB,
            NumReplicas = (uint)settings.NumReplicas,
            FlushEnabled = settings.FlushEnabled,
            ReplicaIndexes = settings.ReplicaIndexes,
            MaxExpirySecs = (uint)settings.MaxTtl
        };

        if (settings.EvictionPolicy.HasValue) createBucketRequest.EvictionMode = settings.EvictionPolicy.Value.ToProto();
        if (settings.CompressionMode.HasValue) createBucketRequest.CompressionMode = settings.CompressionMode.Value.ToProto();
        if (settings.StorageBackend.HasValue) createBucketRequest.StorageBackend = settings.StorageBackend.Value.ToProto();
        if (settings.ConflictResolutionType.HasValue) createBucketRequest.ConflictResolutionType = settings.ConflictResolutionType.Value.ToProto();
        if (settings.DurabilityMinimumLevel.TryConvertToProto(out var protoDurability)) createBucketRequest.MinimumDurabilityLevel = protoDurability;

        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.CancellationToken
        };
        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
        return;

        async Task<CreateBucketResponse> GrpcCall()
        {
            return await _bucketAdminClient.CreateBucketAsync(createBucketRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
        }
    }

    public async Task UpdateBucketAsync(BucketSettings settings, UpdateBucketOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? UpdateBucketOptions.DefaultReadOnly;
        var updateBucketRequest = new UpdateBucketRequest
        {
            BucketName = settings.Name,
            RamQuotaMb = (ulong)settings.RamQuotaMB,
            NumReplicas = (uint)settings.NumReplicas,
            FlushEnabled = settings.FlushEnabled,
            MaxExpirySecs = (uint)settings.MaxTtl
        };
        if (settings.EvictionPolicy.HasValue) updateBucketRequest.EvictionMode = settings.EvictionPolicy.Value.ToProto();
        if (settings.CompressionMode.HasValue) updateBucketRequest.CompressionMode = settings.CompressionMode.Value.ToProto();
        if (settings.DurabilityMinimumLevel.TryConvertToProto(out var protoDurability)) updateBucketRequest.MinimumDurabilityLevel = protoDurability;

        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.CancellationToken
        };
        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
        return;

        async Task<UpdateBucketResponse> GrpcCall()
        {
            return await _bucketAdminClient.UpdateBucketAsync(updateBucketRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
        }
    }

    public async Task DropBucketAsync(string bucketName, DropBucketOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? DropBucketOptions.DefaultReadOnly;
        var dropBucketRequest = new DeleteBucketRequest
        {
            BucketName = bucketName
        };

        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.CancellationToken
        };
        _ = await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
        return;

        async Task<DeleteBucketResponse> GrpcCall()
        {
            return await _bucketAdminClient.DeleteBucketAsync(dropBucketRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
        }
    }

    public async Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(GetAllBucketsOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetAllBucketsOptions.DefaultReadOnly;
        var listBucketsRequest = new ListBucketsRequest();

        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.CancellationToken
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
            return await _bucketAdminClient.ListBucketsAsync(listBucketsRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
        }
    }

    public async Task<BucketSettings> GetBucketAsync(string bucketName, GetBucketOptions? options = null)
    {
        var getAllBucketsOptions = new GetAllBucketsOptions();
        if (options is not null)
        {
            getAllBucketsOptions.CancellationToken(options.TokenValue);
            getAllBucketsOptions.Timeout(options.TimeoutValue);
        }
        var allBuckets = await GetAllBucketsAsync(getAllBucketsOptions).ConfigureAwait(false);
        try
        {
            return allBuckets[bucketName];
        }
        catch (KeyNotFoundException)
        {
            throw new BucketNotFoundException($"Bucket '{bucketName}' not found.");
        }
    }

    public Task FlushBucketAsync(string bucketName, FlushBucketOptions? options = null)=>
        throw ThrowHelper.ThrowFeatureNotAvailableException(nameof(FlushBucketAsync), "Protostellar");
}
#endif
