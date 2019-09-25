using System;
using System.Threading.Tasks;
using Couchbase.Services.KeyValue;

namespace Couchbase.LoadTests.Fixtures
{
    public class ClusterFixture : IDisposable
    {
        public ICluster Cluster { get; }

        public ClusterFixture()
        {
            Cluster = new Cluster("couchbase://localhost", new ClusterOptions()
                .WithBucket("default")
                .WithCredentials("Administrator", "password"));
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
