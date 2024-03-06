#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Retry;
using Couchbase.Management.Buckets;
using Couchbase.Protostellar.Admin.Bucket.V1;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Core.Retry;
using Couchbase.Stellar.Util;
using Couchbase.Utils;

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

        async Task<CreateBucketResponse> grpcCall()
        {
            return await _bucketAdminClient.CreateBucketAsync(createBucketRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.CancellationToken
        };
        _ = await _retryHandler.RetryAsync(grpcCall, stellarRequest).ConfigureAwait(false);
    }

    public async Task UpdateBucketAsync(BucketSettings settings, UpdateBucketOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? UpdateBucketOptions.DefaultReadOnly;
        var updateBucketRequest = new UpdateBucketRequest
        {
            BucketName = settings.Name
        };

        async Task<UpdateBucketResponse> grpcCall()
        {
            return await _bucketAdminClient.UpdateBucketAsync(updateBucketRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.CancellationToken
        };
        _ = await _retryHandler.RetryAsync(grpcCall, stellarRequest).ConfigureAwait(false);

    }

    public async Task DropBucketAsync(string bucketName, DropBucketOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? DropBucketOptions.DefaultReadOnly;
        var dropBucketRequest = new DeleteBucketRequest
        {
            BucketName = bucketName
        };

        async Task<DeleteBucketResponse> grpcCall()
        {
            return await _bucketAdminClient.DeleteBucketAsync(dropBucketRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.CancellationToken
        };
        _ = await _retryHandler.RetryAsync(grpcCall, stellarRequest).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(GetAllBucketsOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetAllBucketsOptions.DefaultReadOnly;
        var listBucketsRequest = new ListBucketsRequest();

        async Task<ListBucketsResponse> grpcCall()
        {
            return await _bucketAdminClient.ListBucketsAsync(listBucketsRequest, _stellarCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);
        }
        var stellarRequest = new StellarRequest
        {
            Idempotent = false,
            Token = opts.CancellationToken
        };
        var response = await _retryHandler.RetryAsync(grpcCall, stellarRequest).ConfigureAwait(false);

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
    }

    public Task<BucketSettings> GetBucketAsync(string bucketName, GetBucketOptions? options = null)=>
        throw ThrowHelper.ThrowFeatureNotAvailableException(nameof(GetBucketAsync), "Protostellar");

    public Task FlushBucketAsync(string bucketName, FlushBucketOptions? options = null)=>
        throw ThrowHelper.ThrowFeatureNotAvailableException(nameof(FlushBucketAsync), "Protostellar");
}
#endif
