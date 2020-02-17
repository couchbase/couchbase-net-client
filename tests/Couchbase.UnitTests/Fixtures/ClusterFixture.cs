using System;
using Couchbase.KeyValue;

namespace Couchbase.UnitTests.Fixtures
{
    public class ClusterFixture : IDisposable
    {
        public ICluster Cluster { get; }

        public ClusterFixture()
        {
            var cluster = new Cluster(new ClusterOptions()
                .WithConnectionString("couchbase://localhost")
                .WithBuckets("default")
                .WithCredentials("Administrator", "password")
            );
            Cluster = cluster;
        }

        public IBucket GetDefaultBucket()
        {
            return Cluster.BucketAsync("default").GetAwaiter().GetResult();
        }

        public ICollection GetDefaultCollection()
        {
            var bucket = GetDefaultBucket();
            return bucket.DefaultCollection();
        }

        public void Dispose()
        {
            Cluster?.Dispose();
        }
    }
}
