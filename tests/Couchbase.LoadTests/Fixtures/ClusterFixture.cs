using System;
using System.Threading.Tasks;
using Couchbase.KeyValue;

namespace Couchbase.LoadTests.Fixtures
{
    public class ClusterFixture : IDisposable
    {
        public ICluster Cluster { get; }

        public ClusterFixture()
        {
            Cluster = new Cluster("couchbase://localhost", new ClusterOptions()
                .Bucket("default")
                .Credentials("Administrator", "password"));
        }

        public async Task<IBucket> GetDefaultBucket()
        {
            return await Cluster.BucketAsync("default");
        }

        public async Task<ICollection> GetDefaultCollection()
        {
            var bucket = await GetDefaultBucket();
            return bucket.DefaultCollection();
        }

        public void Dispose()
        {
            Cluster?.Dispose();
        }
    }
}
