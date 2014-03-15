using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration;
using Couchbase.Core;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class MemcachedBucketTests
    {
        private ICluster _cluster;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _cluster = new Cluster();
        }

        [Test]
        public void Test_OpenBucket()
        {
            var bucket = _cluster.OpenBucket("memcached");
            Assert.IsNotNull(bucket);
        }

        [Test]
        [ExpectedException(typeof(ConfigException))]
        public void Test_That_OpenBucket_Throws_ConfigException_If_Bucket_Does_Not_Exist()
        {
            var bucket = _cluster.OpenBucket("doesnotexist");
            Assert.IsNotNull(bucket);
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _cluster.Dispose();
        }
    }
}
