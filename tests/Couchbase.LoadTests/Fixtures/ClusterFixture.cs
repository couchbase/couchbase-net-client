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
#pragma warning disable CS0618 // Type or member is obsolete
            Cluster = new Cluster(new ClusterOptions()
                .WithConnectionString("couchbase://localhost")
                .WithBuckets("default")
                .WithCredentials("Administrator", "password"));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public async Task<IBucket> GetDefaultBucketAsync()
        {
            return await Cluster.BucketAsync("default");
        }

        public async Task<ICouchbaseCollection> GetDefaultCollectionAsync()
        {
            var bucket = await GetDefaultBucketAsync();
            return await bucket.DefaultCollectionAsync();
        }

        public void Dispose()
        {
            Cluster?.Dispose();
        }
    }
}
