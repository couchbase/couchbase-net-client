using System;
using System.Threading.Tasks;

namespace Couchbase.UnitTests.Fixtures
{
    public class ClusterFixture : IDisposable
    {
        public ICluster Cluster { get; }

        public ClusterFixture()
        {
            var cluster = new Cluster();
            var task = cluster.Initialize(
                new Configuration()
                    .WithServers("couchbase://127.0.0.1")
                    .WithBucket("default")
                    .WithCredentials("Administrator", "password")
            );
            task.ConfigureAwait(false);
            task.Wait();

            Cluster = cluster;
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
            //Cluster?.Dispose();
        }
    }
}
