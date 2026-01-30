using System;
using System.Threading.Tasks;
using Couchbase.KeyValue;

namespace Couchbase.UnitTests.Fixtures
{
    public class ClusterFixture : IDisposable
    {
        public ICluster Cluster { get; }

        public ClusterFixture()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var cluster = new Cluster(new ClusterOptions()
                .WithConnectionString("couchbase://localhost")
                .WithBuckets("default")
                .WithCredentials("Administrator", "password")
            );
#pragma warning restore CS0618 // Type or member is obsolete
            Cluster = cluster;
        }

        public IBucket GetDefaultBucket()
        {
            return Cluster.BucketAsync("default").GetAwaiter().GetResult();
        }


        public async Task<ICouchbaseCollection> GetDefaultCollectionAsync()
        {
            var bucket = GetDefaultBucket();
            return await bucket.DefaultCollectionAsync();
        }

        public void Dispose()
        {
            Cluster?.Dispose();
        }
    }
}
