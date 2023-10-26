using Couchbase.Management.Buckets;
using Couchbase.Protostellar.Admin.Bucket.V1;
using Couchbase.Stellar.Util;
using Grpc.Net.Client;

namespace Couchbase.Stellar.CouchbaseClient.Admin
{

    public class ProtoBucketManager : IBucketManager
    {
        private readonly BucketAdminService.BucketAdminServiceClient _bucketAdminClient;
        public ProtoBucketManager(GrpcChannel channel)
        {
            _bucketAdminClient = new BucketAdminService.BucketAdminServiceClient(channel);
        }

        public async Task CreateBucketAsync(BucketSettings settings, CreateBucketOptions? options = null)
        {
            options ??= CreateBucketOptions.Default;
            var createBucketRequest = new CreateBucketRequest
            {
                BucketName = settings.Name
            };

            //TODO: Only Grpc CallOptions can be passed and we don't have access to the options' CancellationToken : applies to all calls
            await _bucketAdminClient.CreateBucketAsync(createBucketRequest, new Grpc.Core.CallOptions())
                .ConfigureAwait(false);
        }

        public async Task UpdateBucketAsync(BucketSettings settings, UpdateBucketOptions? options = null)
        {
            options ??= UpdateBucketOptions.Default;
            var updateBucketRequest = new UpdateBucketRequest
            {
                BucketName = settings.Name
            };

            await _bucketAdminClient.UpdateBucketAsync(updateBucketRequest, new Grpc.Core.CallOptions())
                .ConfigureAwait(false);

        }

        public async Task DropBucketAsync(string bucketName, DropBucketOptions? options = null)
        {
            options ??= DropBucketOptions.Default;
            var dropBucketRequest = new DeleteBucketRequest
            {
                BucketName = bucketName
            };

            await _bucketAdminClient.DeleteBucketAsync(dropBucketRequest, new Grpc.Core.CallOptions())
                .ConfigureAwait(false);
        }

        public Task<BucketSettings> GetBucketAsync(string bucketName, GetBucketOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(GetAllBucketsOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task FlushBucketAsync(string bucketName, FlushBucketOptions? options = null)
        {
            throw new NotImplementedException();
        }

        private Grpc.Core.CallOptions GrpcCallOptions(TimeSpan? timeout, CancellationToken cancellationToken) =>
            new (deadline: timeout.FromNow(), cancellationToken: cancellationToken);
    }
}
