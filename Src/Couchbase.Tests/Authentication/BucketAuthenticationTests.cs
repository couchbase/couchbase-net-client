using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using NUnit.Framework;
using System.Collections.Generic;

namespace Couchbase.Tests.Authentication
{
    [TestFixture]
    public class BucketAuthenticationTests
    {
        [Test]
        public void When_Valid_Credentials_Provided_Bucket_Created_Succesfully()
        {
            var cluster = new Cluster(new ClientConfiguration
            {
                BucketConfigs = new List<BucketConfiguration>
                {
                    new BucketConfiguration
                    {
                        BucketName = "authenticated"
                    }
                }
            });
            var bucket = cluster.OpenBucket("authenticated", "secret");
            Assert.IsNotNull(bucket);
        }

        [Test]
        [ExpectedException(typeof(ConfigException))]
        public void When_InValid_Credentials_Provided_Bucket_Created_UnSuccesfully()
        {
            var cluster = new Cluster(new ClientConfiguration
            {
                BucketConfigs = new List<BucketConfiguration>
                {
                    new BucketConfiguration
                    {
                        BucketName = "authenticated"
                    }
                }
            });
            var bucket = cluster.OpenBucket("authenticated", "secretw");
            Assert.IsNotNull(bucket);
        }
    }
}