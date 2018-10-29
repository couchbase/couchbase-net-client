using Couchbase.IntegrationTests.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    public class CouchbaseBucketAlternateNetworkTests
    {
        [Test]
        [Ignore("Requires CB cluster configured with external services, eg configured in docker. " +
                "Update host is config.json and KV port in 'alternateNetwork' section.")]
        public void When_using_alternate_networks_can_perform_kv_operations()
        {
            const string key = "When_using_alternate_networks_can_perform_kv_operations";

            var configuration = TestConfiguration.GetConfiguration("alternateNetwork");
            using (var cluster = new Cluster(configuration))
            {
                cluster.SetupEnhancedAuth();

                var bucket = cluster.OpenBucket("default");
                try
                {
                    var insertResult = bucket.Insert(key, new { });
                    Assert.IsTrue(insertResult.Success);

                    var getResult = bucket.Get<dynamic>(key);
                    Assert.IsTrue(getResult.Success);
                }
                finally
                {
                    var removeResult = bucket.Remove(key);
                    Assert.IsTrue(removeResult.Success);
                }
            }
        }
    }
}
