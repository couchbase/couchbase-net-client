using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public interface IBucketManager
    {
        IEnumerable<IBucket> GetAll();

        Task Insert(string bucketName, BucketManagerOptions options);

        Task Upsert(string bucketName, BucketManagerOptions options);

        Task Remove(string bucketName);
        
        Task Flush();
    }
}
