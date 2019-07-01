using System;
using System.Threading.Tasks;

namespace Couchbase.UnitTests.Fixtures
{
    public class ClusterFixture : IDisposable
    {
        public ICluster Cluster { get; }

        public ClusterFixture()
        {
            var cluster = new Cluster(new Configuration()
                    .WithServers("couchbase://localhost")
                    .WithBucket("default")
                    .WithCredentials("Administrator", "password")
            );
            Cluster = cluster;
        }

        public async Task<IBucket> GetDefaultBucket()
        {
            return await Cluster.BucketAsync("default");
        }

        public async Task<ICollection> GetDefaultCollection()
        {
            var bucket = await GetDefaultBucket();
            return await bucket.DefaultCollectionAsync();
        }

        public void Dispose()
        {
            //Cluster?.Dispose();
        }
    }
}
