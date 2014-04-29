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
            Cluster.Initialize(new ClientConfiguration
            {
                BucketConfigs = new List<BucketConfiguration>
                {
                    new BucketConfiguration
                    {
                        BucketName = "authenticated"
                    }
                }
            });

            var cluster = Cluster.Get();
            var bucket = cluster.OpenBucket("authenticated", "secret");
            cluster.CloseBucket(bucket);
            Assert.IsNotNull(bucket);
            cluster.CloseBucket(bucket);
        }

        [Test]
        [ExpectedException(typeof(ConfigException))]
        public void When_InValid_Credentials_Provided_Bucket_Created_UnSuccesfully()
        {
            Cluster.Initialize(new ClientConfiguration
            {
                BucketConfigs = new List<BucketConfiguration>
                {
                    new BucketConfiguration
                    {
                        BucketName = "authenticated"
                    }
                }
            });
            var cluster = Cluster.Get();
            var bucket = cluster.OpenBucket("authenticated", "secretw"); 
            cluster.CloseBucket(bucket);
            Assert.IsNotNull(bucket);
        }
    }
}