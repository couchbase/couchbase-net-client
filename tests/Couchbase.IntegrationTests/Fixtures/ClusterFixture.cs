using System;
using System.Threading.Tasks;
using Couchbase.KeyValue;

namespace Couchbase.IntegrationTests.Fixtures
{
    public class ClusterFixture : IDisposable
    {
        public ICluster Cluster { get; }

        public ClusterOptions ClusterOptions { get; }

        public ClusterFixture()
        {
            ClusterOptions = new ClusterOptions()
                .WithBucket("default")
                .WithCredentials("Administrator", "password");

            Cluster = Couchbase.Cluster.Connect("couchbase://localhost", ClusterOptions);
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
