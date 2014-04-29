using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Buckets;
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
        public void TestFixtureSetUp()
        {
            Cluster.Initialize();
            _cluster = Cluster.Get();
        }

        [Test]
        public void Test_GetBucket()
        {
            var bucket = _cluster.OpenBucket("default");
            Assert.AreEqual("default", bucket.Name);
           // var bucket2 = _cluster.OpenBucket("default");
            _cluster.CloseBucket(bucket);
        }

        [Test]
        [ExpectedException(typeof(ConfigException))]
        public void Test_That_GetBucket_Throws_ConfigException_If_Bucket_Does_Not_Exist()
        {
            var bucket = _cluster.OpenBucket("doesnotexist");
            Assert.AreEqual("doesnotexist", bucket.Name);
            _cluster.CloseBucket(bucket);
        }

        [Test]
        public void Test_That_Bucket_Can_Be_Opened_When_Not_Configured()
        {
            var bucket = _cluster.OpenBucket("authenticated", "secret");
            Assert.IsNotNull(bucket);
            _cluster.CloseBucket(bucket);
        }

        [Test]
        [ExpectedException(typeof(ConfigException))]
        public void Test_That_Bucket_That_Doesnt_Exist_Throws_ConfigException()
        {
            var bucket = _cluster.OpenBucket("authenicated", "secret");
            Assert.IsNotNull(bucket);
            _cluster.CloseBucket(bucket);
        }

        [Test]
        public void Test_View_Query()
        {
            var bucket = (IViewSupportable)_cluster.OpenBucket("beer-sample");
            var query = new ViewQuery(true).
                From("beer-sample", "beer").
                View("brewery_beers").
                Limit(10);

            var result = bucket.Get<dynamic>(query);
            Assert.Greater(result.TotalRows, 0);
            _cluster.CloseBucket((IBucket)bucket);
        }

        [Test]
        public void Test_View_Query_Lots()
        {
            var bucket = (IViewSupportable)_cluster.OpenBucket("beer-sample");
            var query = new ViewQuery(false).
                From("beer-sample", "beer").
                View("brewery_beers");

            var result = bucket.Get<dynamic>(query);
            for (var i = 0; i < 10; i++)
            {
                using (new OperationTimer())
                {       
                    Assert.Greater(result.TotalRows, 0);
                }
            }
            _cluster.CloseBucket((IBucket)bucket);
        }

        [Test]
        public void Test_N1QL_Query()
        {
            var bucket = (ICouchbaseBucket) _cluster.OpenBucket("default");

            const string query = "SELECT * FROM tutorial WHERE fname = 'Ian'";

            var result = bucket.Query<dynamic>(query);
            foreach (var row in result.Rows)
            {
                Console.WriteLine(row);
            }
            _cluster.CloseBucket(bucket);
        }

        [Test]
        public void Test_GetAsync()
        {
            const string key = "asynckey";
            const string value = "asyncvalue";

            Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
            var bucket = (ICouchbaseBucket)_cluster.OpenBucket("default");
            var setResult= bucket.Insert(key, value);
            Assert.IsTrue(setResult.Success);
            var getResult = bucket.GetAsync<string>(key);
            getResult.Wait();
            Assert.AreEqual(value, getResult.Result.Value);
            _cluster.CloseBucket(bucket);
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _cluster.Dispose();
        }
    }
}
