using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Management.Buckets
{
    public interface IBucketManager
    {
        Task CreateBucketAsync(BucketSettings settings, CreateBucketOptions? options = null);

        Task UpdateBucketAsync(BucketSettings settings, UpdateBucketOptions? options = null);

        Task DropBucketAsync(string bucketName, DropBucketOptions? options = null);

        Task<BucketSettings> GetBucketAsync(string bucketName, GetBucketOptions? options = null);

        Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(GetAllBucketsOptions? options = null);

        Task FlushBucketAsync(string bucketName, FlushBucketOptions? options = null);
    }
}
