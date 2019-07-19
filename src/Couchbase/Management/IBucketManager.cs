using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public interface IBucketManager
    {
        Task CreateAsync(BucketSettings settings, CreateBucketOptions options);

        Task UpsertAsync(BucketSettings settings, UpsertBucketOptions options);

        Task DropAsync(string bucketName, DropBucketOptions options);

        Task<BucketSettings> GetAsync(string bucketName, GetBucketOptions options);

        Task<Dictionary<string, BucketSettings>> GetAllAsync(GetAllBucketOptions options);

        Task Flush(string bucketName, FlushBucketOptions options);
    }
}
