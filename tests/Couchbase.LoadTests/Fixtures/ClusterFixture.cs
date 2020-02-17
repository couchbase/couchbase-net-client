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
            Cluster = new Cluster(new ClusterOptions()
                .WithConnectionString("couchbase://localhost")
                .WithBuckets("default")
                .WithCredentials("Administrator", "password"));
        }

        public async Task<IBucket> GetDefaultBucketAsync()
        {
            return await Cluster.BucketAsync("default");
        }

        public async Task<ICollection> GetDefaultCollection()
        {
            var bucket = await GetDefaultBucketAsync();
            return bucket.DefaultCollection();
        }

        public void Dispose()
        {
            Cluster?.Dispose();
        }
    }
}
