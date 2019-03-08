using System;
using System.Threading.Tasks;

namespace Couchbase.IntegrationTests.Fixtures
{
    public class ClusterFixture : IDisposable
    {
        public ICluster Cluster { get; }

        public ClusterFixture()
        {
            Cluster = new Cluster(new Configuration()
                .WithServers("couchbase://localhost")
                .WithBucket("default")
                .WithCredentials("Administrator", "password"));
        }

        public async Task<IBucket> GetDefaultBucket()
        {
            return await Cluster.Bucket("default");
        }

        public async Task<ICollection> GetDefaultCollection()
        {
            var bucket = await GetDefaultBucket();
            return await bucket.DefaultCollection;
        }

        public void Dispose()
        {
            Cluster?.Dispose();
        }
    }
}
