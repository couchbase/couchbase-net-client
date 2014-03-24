using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration;
using Couchbase.Core;
using Couchbase.Views;
using NUnit.Framework;
using Wintellect;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class CouchbaseBucketTests
    {
        private ICluster _cluster;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _cluster = new Cluster();
        }

        [Test]
        public void Test_GetBucket()
        {
            var bucket = _cluster.OpenBucket("default");
            Assert.AreEqual("default", bucket.Name);
        }

        [Test]
        [ExpectedException(typeof(ConfigException))]
        public void Test_That_GetBucket_Throws_ConfigException_If_Bucket_Does_Not_Exist()
        {
            var bucket = _cluster.OpenBucket("doesnotexist");
            Assert.AreEqual("doesnotexist", bucket.Name);
        }

        [Test]
        public void Test_View_Query()
        {
            var bucket = (IViewSupportable)_cluster.OpenBucket("default");
            var query = new ViewQuery(false).
                From("default", "cities").
                View("by_name");

            var result = bucket.Get<dynamic>(query);
            Assert.Greater(result.TotalRows, 0);
        }

        [Test]
        public void Test_View_Query_Lots()
        {
            var bucket = (IViewSupportable)_cluster.OpenBucket("default");
            var query = new ViewQuery(false).
                From("default", "cities").
                View("by_name");

            for (var i = 0; i < 10; i++)
            {
                using (new OperationTimer())
                {
                    var result = bucket.Get<dynamic>(query);
                    Assert.Greater(result.TotalRows, 0);
                }
            }
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _cluster.Dispose();
        }
    }
}
