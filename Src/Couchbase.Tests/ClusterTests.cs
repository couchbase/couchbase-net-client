using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Client.Providers;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class ClusterTests
    {
        [Test]
        public void When_Bucket_Is_Open_IsOpen_Returns_True()
        {
            var cluster = new Cluster("couchbaseClients/couchbase");
            var bucket = cluster.OpenBucket("default");
            Assert.IsTrue(cluster.IsOpen("default"));
        }

        [Test]
        public void When_Bucket_Is_Not_Open_IsOpen_Returns_False()
        {
            var cluster = new Cluster("couchbaseClients/couchbase");
            var bucket = cluster.OpenBucket("default");
            cluster.CloseBucket(bucket);
            Assert.IsFalse(cluster.IsOpen("default"));
        }

        [Test]
        public void When_Bucket_Is_Closed_By_Dispose_IsOpen_Returns_False()
        {
            var cluster = new Cluster("couchbaseClients/couchbase");
            var bucket = cluster.OpenBucket("default");
            bucket.Dispose();
            Assert.IsFalse(cluster.IsOpen("default"));
        }

        [Test]
        [Category("Integration")]
        public void When_Configuration_Contains_Bad_Bucket_Password_It_Is_Used_And_Fails()
        {
            var config = new ClientConfiguration((CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase"));
            config.BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {
                    "default",
                    new BucketConfiguration
                    {
                        BucketName = "default",
                        Password = "secret"
                    }
                }
            };

            var cluster = new Cluster(config);
            var bucketName = config.BucketConfigs.Single().Value.BucketName;
            try
            {
                var bucket = cluster.OpenBucket(bucketName);
                Assert.Fail("Unexpected GetBucket success");
            }
            catch (AggregateException e)
            {
                e = e.Flatten();
                if (!(e.InnerException is AuthenticationException))
                {
                    Assert.Fail("Expected authentication exception, got " + e.InnerException);
                }
                //success
            }
        }
    }
}
