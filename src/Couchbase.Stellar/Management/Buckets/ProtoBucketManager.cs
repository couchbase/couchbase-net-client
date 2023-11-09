using System.Text.Json;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Protostellar.Admin.Bucket.V1;
using Couchbase.Stellar.Core;
using BucketType = Couchbase.Management.Buckets.BucketType;
using CompressionMode = Couchbase.Management.Buckets.CompressionMode;
using ConflictResolutionType = Couchbase.Management.Buckets.ConflictResolutionType;
using StorageBackend = Couchbase.Management.Buckets.StorageBackend;

namespace Couchbase.Stellar.Management.Buckets;

internal class ProtoBucketManager : IBucketManager
{
    private readonly BucketAdminService.BucketAdminServiceClient _bucketAdminClient;
    private readonly ProtoCluster _protoCluster;
    public ProtoBucketManager(ProtoCluster protoCluster)
    {
        _protoCluster = protoCluster;
        _bucketAdminClient = new BucketAdminService.BucketAdminServiceClient(protoCluster.GrpcChannel);
    }

    public async Task CreateBucketAsync(BucketSettings settings, CreateBucketOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? CreateBucketOptions.DefaultReadOnly;
        var createBucketRequest = new CreateBucketRequest
        {
            BucketName = settings.Name
        };
        await _bucketAdminClient.CreateBucketAsync(createBucketRequest, _protoCluster.GrpcCallOptions(opts.CancellationToken))
            .ConfigureAwait(false);
    }

    public async Task UpdateBucketAsync(BucketSettings settings, UpdateBucketOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? UpdateBucketOptions.DefaultReadOnly;
        var updateBucketRequest = new UpdateBucketRequest
        {
            BucketName = settings.Name
        };

        await _bucketAdminClient.UpdateBucketAsync(updateBucketRequest, _protoCluster.GrpcCallOptions(opts.CancellationToken))
            .ConfigureAwait(false);

    }

    public async Task DropBucketAsync(string bucketName, DropBucketOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? DropBucketOptions.DefaultReadOnly;
        var dropBucketRequest = new DeleteBucketRequest
        {
            BucketName = bucketName
        };

        await _bucketAdminClient.DeleteBucketAsync(dropBucketRequest, _protoCluster.GrpcCallOptions(opts.CancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(GetAllBucketsOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? GetAllBucketsOptions.DefaultReadOnly;
        var listBucketsRequest = new ListBucketsRequest();
        var response = await _bucketAdminClient.ListBucketsAsync(listBucketsRequest, _protoCluster.GrpcCallOptions(opts.CancellationToken)).ConfigureAwait(false);

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

    public Task<BucketSettings> GetBucketAsync(string bucketName, GetBucketOptions? options = null)
    {
        throw new NotImplementedException();
    }

    public Task FlushBucketAsync(string bucketName, FlushBucketOptions? options = null)
    {
        throw new NotImplementedException();
    }
}
