using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration;
using Couchbase.Core;
using Couchbase.IO;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class MemcachedBucketTests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _cluster = new Cluster();
        }

        [Test]
        public void Test_OpenBucket()
        {
            _bucket = _cluster.OpenBucket("memcached");
            Assert.IsNotNull(_bucket);
        }

        [Test]
        [ExpectedException(typeof(ConfigException))]
        public void Test_That_OpenBucket_Throws_ConfigException_If_Bucket_Does_Not_Exist()
        {
            var bucket = _cluster.OpenBucket("doesnotexist");
            Assert.IsNotNull(bucket);
        }

        [Test]
        public void Test_Insert_With_String()
        {
            const int zero = 0;
            const string key = "memkey1";
            const string value = "somedata";

            _bucket = _cluster.OpenBucket("memcached");
            var result = _bucket.Insert(key, value);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(string.Empty, result.Message);
            Assert.AreEqual(string.Empty, result.Value);
            Assert.Greater(result.Cas, zero);
            
        }

        [Test]
        public void Test_Get_With_String()
        {
            const int zero = 0;
            const string key = "memkey1";
            const string value = "somedata";

            _bucket = _cluster.OpenBucket("memcached");
            var result = _bucket.Get<string>(key);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.AreEqual(string.Empty, result.Message);
            Assert.AreEqual(value, result.Value);
            Assert.Greater(result.Cas, zero);
        }

        [TearDown]
        public void TearDown()
        {
            if (_bucket == null)
            {
                //noop
            }
            else
            {
                _cluster.CloseBucket(_bucket);
                _bucket = null;
            }
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _cluster.Dispose();
        }
    }
}
