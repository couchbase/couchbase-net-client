using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management.Buckets
{
    public interface IBucketManager
    {
        Task CreateBucketAsync(BucketSettings settings, CreateBucketOptions options);

        Task UpsertBucketAsync(BucketSettings settings, UpsertBucketOptions options);

        Task DropBucketAsync(string bucketName, DropBucketOptions options);

        Task<BucketSettings> GetBucketAsync(string bucketName, GetBucketOptions options);

        Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(GetAllBucketsOptions options);

        Task FlushBucketAsync(string bucketName, FlushBucketOptions options);
    }
}
